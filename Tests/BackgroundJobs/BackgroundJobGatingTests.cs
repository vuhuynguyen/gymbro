using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.IdentityModule.Infrastructure.Services;
using NSubstitute;
using WebApi.Composition;
using Xunit;

namespace Gymbro.Tests.BackgroundJobs;

/// <summary>
/// The background maintenance jobs must not query the database when disabled, and default to cadences that
/// keep the database idle enough for a serverless Postgres to scale to zero. "Touches the database" is proven
/// via the <see cref="IServiceScopeFactory"/>: every one of these services opens a DI scope before any DB
/// access, so a service that never creates a scope never touched the database.
/// </summary>
public sealed class BackgroundJobGatingTests
{
    [Fact]
    public async Task Disabled_outbox_cleanup_never_opens_a_scope()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var options = Options.Create(new OutboxOptions { CleanupEnabled = false });
        var service = new OutboxCleanupService(scopeFactory, options, NullLogger<OutboxCleanupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public async Task Disabled_reconciliation_never_opens_a_scope()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var options = Options.Create(new ReconciliationOptions { Enabled = false });
        var service = new CrossStoreReconciliationService(
            scopeFactory, options, NullLogger<CrossStoreReconciliationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public async Task Disabled_nutrition_day_closer_never_opens_a_scope()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var options = Options.Create(new NutritionMaintenanceOptions { DayCloseEnabled = false });
        var service = new NutritionStaleDayCloser(scopeFactory, options, NullLogger<NutritionStaleDayCloser>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public async Task Disabled_refresh_token_cleanup_never_opens_a_scope()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshTokenCleanupEnabled"] = "false" })
            .Build();
        var service = new RefreshTokenCleanupService(
            scopeFactory, configuration, NullLogger<RefreshTokenCleanupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public void Defaults_are_tuned_for_scale_to_zero()
    {
        // Reconciliation is opt-in (read-only drift detector, guarded by the cross-store transaction).
        Assert.False(new ReconciliationOptions().Enabled);

        // Outbox dispatch is signal-driven; the fallback + purge are rare safety nets, not tight polls.
        var outbox = new OutboxOptions();
        Assert.Equal(TimeSpan.FromHours(6), outbox.FallbackInterval);
        Assert.Equal(TimeSpan.FromHours(24), outbox.CleanupInterval);
        Assert.True(outbox.CleanupEnabled);

        // The day-close sweep no longer polls every 15 minutes.
        var nutrition = new NutritionMaintenanceOptions();
        Assert.True(nutrition.DayCloseEnabled);
        Assert.Equal(TimeSpan.FromHours(6), nutrition.DayCloseInterval);
    }
}
