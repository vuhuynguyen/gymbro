using System.Diagnostics.Metrics;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.IdentityModule.Infrastructure.Identity;

namespace WebApi.Composition;

/// <summary>
/// Read-only background check that surfaces drift between the two stores that are linked only by the
/// convention <c>AppUser.DomainUserId == User.Id</c> (no cross-store FK). In a healthy system every
/// <c>AppUser</c> maps to exactly one live (non-deleted) domain <c>User</c> and vice versa — the cross-store
/// transaction guarantees this at write time. This service is the durable safety net: it
/// only DETECTS and reports drift (logs + metrics); it never mutates data, so a transient miscount can't
/// cascade into accidental deletes.
///
/// <para><b>Lifecycle note:</b> admin-delete soft-deletes the domain <c>User</c> but hard-deletes the
/// <c>AppUser</c> in the same transaction, so a soft-deleted <c>User</c> legitimately has no <c>AppUser</c>.
/// We therefore reconcile only against <i>live</i> users to avoid false positives for deleted accounts.</para>
/// </summary>
public sealed class CrossStoreReconciliationService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReconciliationOptions> options,
    ILogger<CrossStoreReconciliationService> logger) : BackgroundService
{
    private static readonly Meter Meter = new("GymBro.Reconciliation");

    // Static so a single observable-gauge registration reports the latest run's result (one hosted instance).
    private static long _orphanedAppUsers;
    private static long _liveUsersMissingAppUser;

    private static readonly ObservableGauge<long> OrphanedAppUsersGauge =
        Meter.CreateObservableGauge("reconciliation.orphaned_app_users", () => _orphanedAppUsers);
    private static readonly ObservableGauge<long> LiveUsersMissingAppUserGauge =
        Meter.CreateObservableGauge("reconciliation.live_users_missing_app_user", () => _liveUsersMissingAppUser);

    private readonly ReconciliationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        try
        {
            await Task.Delay(_options.InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.Interval);

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
            var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            // Live domain users (the global filter already excludes soft-deleted; User is not tenant-scoped,
            // so a null-tenant background scope still sees every live user).
            var liveUserIds = (await appDb.Users
                    .Select(u => u.Id)
                    .ToListAsync(stoppingToken))
                .ToHashSet();

            var appUserDomainIds = await identityDb.Set<AppUser>()
                .Select(a => a.DomainUserId)
                .ToListAsync(stoppingToken);
            var appUserDomainSet = appUserDomainIds.ToHashSet();

            // AppUser pointing at no live domain User (its User is missing or was deleted without cleanup).
            var orphanedAppUsers = appUserDomainIds.Count(
                id => id == Guid.Empty || !liveUserIds.Contains(id));

            // Live domain User with no AppUser (registration provisioning half-completed).
            var liveUsersMissingAppUser = liveUserIds.Count(id => !appUserDomainSet.Contains(id));

            _orphanedAppUsers = orphanedAppUsers;
            _liveUsersMissingAppUser = liveUsersMissingAppUser;

            if (orphanedAppUsers > 0 || liveUsersMissingAppUser > 0)
                logger.LogWarning(
                    "Cross-store drift detected: {OrphanedAppUsers} AppUser row(s) reference no live domain "
                    + "User, and {LiveUsersMissingAppUser} live domain User(s) have no AppUser. These should be "
                    + "zero (the cross-store transaction keeps both stores in step) — investigate before it "
                    + "compounds. See docs/DATABASE.md.",
                    orphanedAppUsers,
                    liveUsersMissingAppUser);
            else
                logger.LogDebug(
                    "Cross-store reconciliation clean: {LiveUsers} live user(s), no drift.",
                    liveUserIds.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cross-store reconciliation failed; will retry on the next tick.");
        }
    }
}
