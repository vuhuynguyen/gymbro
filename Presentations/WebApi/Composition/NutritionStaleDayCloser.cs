using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Entities;

namespace WebApi.Composition;

/// <summary>
/// Closes nutrition days at the trainee's local midnight, in the background — so closing is never a side effect
/// of a READ (it used to run inside <c>GetMyNutritionTodayHandler</c>), and a day finalizes (marking still-Planned
/// items Missed, computing adherence, raising <c>DailyLogClosedEvent</c> via the outbox) even if the trainee never
/// reopens the app. "Stale" is decided per-day against the day's captured zone: a day is closed once its
/// <c>LocalDate</c> is strictly before the trainee's current local date.
/// </summary>
public sealed class NutritionStaleDayCloser(
    IServiceScopeFactory scopeFactory,
    ILogger<NutritionStaleDayCloser> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTimeOffset.UtcNow;
            var utcToday = DateOnly.FromDateTime(now.UtcDateTime);

            // SQL candidate filter: any open day on or before UTC-today MIGHT be past in the trainee's zone (zones
            // run up to ~+14h ahead of UTC), so include UTC-today; the per-day zone check below decides. This is a
            // system sweep with no tenant/user context, so the global filters are deliberately ignored.
            var candidates = await db.DailyNutritionLogs
                .IgnoreQueryFilters()
                .Where(l => !l.IsDeleted && l.Status == DailyLogStatus.Open && l.LocalDate <= utcToday)
                .Include(l => l.Items)
                .ToListAsync(stoppingToken);

            var closed = 0;
            foreach (var day in candidates)
            {
                var traineeToday = LocalDayResolver.LocalDateOf(now, day.ClientTimezone);
                if (day.LocalDate < traineeToday)
                {
                    day.Close();
                    closed++;
                }
            }

            if (closed > 0)
            {
                await db.SaveChangesAsync(stoppingToken); // drains DailyLogClosedEvent(s) into the outbox
                logger.LogInformation("Closed {Count} stale nutrition day(s) at local midnight.", closed);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Nutrition stale-day close failed; will retry on the next tick.");
        }
    }
}
