using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;

namespace Modules.NutritionModule.Application.Queries.Handlers;

/// <summary>
/// The caller's own body-metric trend (api/me/progress/metrics/series, Phase 2), self-scoped to
/// currentUser.UserId. Reads one metric type over a local-date range (default: trailing 12 weeks) via
/// <see cref="IMetricEntryRepository.GetOwnSeriesAsync"/> (own-scoped, case-insensitive type match), then
/// collapses to ONE point per local day = the LATEST check-in that day (the repository returns entries
/// LocalDate asc, LoggedAtUtc asc, so the last entry per day wins). Returns 200 with empty Points (never 404)
/// when the caller logged nothing of that type in range.
/// </summary>
public sealed class GetMyMetricSeriesHandler(
    IMetricEntryRepository metricRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyMetricSeriesQuery, Result<MetricSeriesDto>>
{
    private const int DefaultWindowWeeks = 12;

    public async Task<Result<MetricSeriesDto>> Handle(
        GetMyMetricSeriesQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedType = (request.Type ?? string.Empty).Trim().ToLowerInvariant();

        var today = LocalDayResolver.LocalDateOf(DateTimeOffset.UtcNow, currentUser.TimeZoneId);
        var to = request.To ?? today;
        // Default window: trailing 12 weeks ending at `to` (inclusive) when no `from` is given.
        var from = request.From ?? to.AddDays(-7 * DefaultWindowWeeks + 1);

        var entries = await metricRepository.GetOwnSeriesAsync(
            currentUser.UserId, normalizedType, from, to, cancellationToken);

        // Latest-per-local-day: entries arrive LocalDate asc, LoggedAtUtc asc, so the last one on a day is the
        // newest check-in. GroupBy preserves first-seen order, and Last() within a group takes that newest.
        var points = entries
            .GroupBy(e => e.LocalDate)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var latest = g.Last();
                return new MetricSeriesPointDto(latest.LocalDate, latest.Value);
            })
            .ToList();

        // Unit echoes the most-recent non-null unit in range (null when none logged); the type echoes the
        // normalized request, so the client always gets back exactly what it asked for.
        var unit = entries
            .Where(e => e.Unit is not null)
            .OrderByDescending(e => e.LoggedAtUtc)
            .Select(e => e.Unit)
            .FirstOrDefault();

        return Result<MetricSeriesDto>.Success(new MetricSeriesDto(normalizedType, unit, points));
    }
}
