using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Outbox;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.WorkoutSessionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Outbox;

/// <summary>
/// End-to-end wiring check for the outbox wake signal: builds a real DI container the way the app does
/// (<c>AddDbContext</c> + a singleton <see cref="IOutboxSignal"/>) and proves EF injects that singleton into
/// <see cref="AppDbContext"/> and that a commit which writes domain events raises exactly one wake — while an
/// empty save raises none (so an idle app never touches the signal, and the database can scale to zero).
/// </summary>
public sealed class OutboxSignalWiringTests
{
    [Fact]
    public async Task Committing_domain_events_signals_the_registered_singleton_once_per_write()
    {
        var spy = new SpyOutboxSignal();

        var services = new ServiceCollection();
        services.AddSingleton<IOutboxSignal>(spy);
        services.AddScoped<IDbContextServices, StubDbContextServices>();
        foreach (var configuration in TestModelConfigurations.All())
            services.AddSingleton(configuration);
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"signal-wiring-{Guid.NewGuid()}"));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = WorkoutSession.Start(
            Guid.NewGuid(), Guid.NewGuid(), SessionSource.Adhoc, null, null, "Push Day", null, "UTC", null);
        db.Set<WorkoutSession>().Add(session);
        await db.SaveChangesAsync();

        // A write that commits a domain event wakes the processor exactly once.
        var beforeComplete = spy.NotifyCount;
        session.Complete(null, null, null, prCount: 0); // raises SessionCompletedEvent
        await db.SaveChangesAsync();
        Assert.Equal(beforeComplete + 1, spy.NotifyCount);

        // A save with nothing to persist writes no outbox rows and must NOT wake the processor — this is
        // what keeps an idle app from touching the database.
        var beforeNoop = spy.NotifyCount;
        await db.SaveChangesAsync();
        Assert.Equal(beforeNoop, spy.NotifyCount);
    }

    private sealed class SpyOutboxSignal : IOutboxSignal
    {
        private int _notifyCount;
        public int NotifyCount => Volatile.Read(ref _notifyCount);
        public void Notify() => Interlocked.Increment(ref _notifyCount);
        public Task WaitAsync(TimeSpan fallbackInterval, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Admin context services so EF global filters never exclude the rows under test.</summary>
    private sealed class StubDbContextServices : IDbContextServices, ICurrentUser, ITenantContext
    {
        public ICurrentUser CurrentUser => this;
        public ITenantContext TenantContext => this;
        public Guid UserId => Guid.Empty;
        public bool IsAdmin => true;
        public string? TimeZoneId => null;
        public Guid? TenantId => null;
    }
}
