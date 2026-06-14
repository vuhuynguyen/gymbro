using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Outbox;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Outbox;

/// <summary>
/// Verifies the F2 transactional outbox without a database engine (EF InMemory): domain events are drained
/// into the outbox on SaveChanges, and the dispatcher publishes pending rows at-least-once (marking success,
/// recording failures, and respecting the poison-message attempt cap).
/// </summary>
public sealed class OutboxTests
{
    private static AppDbContext NewDb() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"outbox-{Guid.NewGuid()}")
                .Options,
            new StubDbContextServices(),
            TestModelConfigurations.All());

    private static SessionCompletedEvent SampleEvent() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

    [Fact]
    public void Serializer_round_trips_a_domain_event()
    {
        var original = SampleEvent();

        var message = OutboxMessage.Create(original, DateTime.UtcNow);
        var rehydrated = Assert.IsType<SessionCompletedEvent>(OutboxSerializer.Deserialize(message));

        Assert.Equal(original, rehydrated);
    }

    [Fact]
    public async Task SaveChanges_drains_domain_events_into_the_outbox_and_clears_them()
    {
        await using var db = NewDb();

        var session = WorkoutSession.Start(
            Guid.NewGuid(), Guid.NewGuid(), SessionSource.Adhoc, null, null, "Push Day", null, "UTC", null);
        db.Set<WorkoutSession>().Add(session);
        await db.SaveChangesAsync();

        session.Complete(null, null, null, prCount: 0); // raises SessionCompletedEvent
        await db.SaveChangesAsync();

        var outbox = await db.OutboxMessages.ToListAsync();
        var message = Assert.Single(outbox);
        Assert.Contains(nameof(SessionCompletedEvent), message.Type);
        Assert.Null(message.ProcessedOnUtc);
        Assert.Empty(session.DomainEvents); // drained, not double-published
    }

    [Fact]
    public async Task Dispatcher_publishes_pending_messages_and_marks_them_processed()
    {
        await using var db = NewDb();
        db.OutboxMessages.Add(OutboxMessage.Create(SampleEvent(), DateTime.UtcNow));
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IPublisher>();
        var dispatcher = new OutboxDispatcher(db, publisher, NullLogger<OutboxDispatcher>.Instance);

        var dispatched = await dispatcher.ProcessBatchAsync(batchSize: 50, maxAttempts: 10, CancellationToken.None);

        Assert.Equal(1, dispatched);
        await publisher.Received(1).Publish(
            Arg.Is<INotification>(n => n is SessionCompletedEvent),
            Arg.Any<CancellationToken>());

        var message = await db.OutboxMessages.SingleAsync();
        Assert.NotNull(message.ProcessedOnUtc);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.Error);
    }

    [Fact]
    public async Task Dispatcher_records_failure_and_leaves_message_pending_for_retry()
    {
        await using var db = NewDb();
        db.OutboxMessages.Add(OutboxMessage.Create(SampleEvent(), DateTime.UtcNow));
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IPublisher>();
        publisher
            .When(p => p.Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("handler boom"));
        var dispatcher = new OutboxDispatcher(db, publisher, NullLogger<OutboxDispatcher>.Instance);

        var dispatched = await dispatcher.ProcessBatchAsync(batchSize: 50, maxAttempts: 10, CancellationToken.None);

        Assert.Equal(0, dispatched);
        var message = await db.OutboxMessages.SingleAsync();
        Assert.Null(message.ProcessedOnUtc);
        Assert.Equal(1, message.Attempts);
        Assert.Contains("handler boom", message.Error);
    }

    [Fact]
    public async Task Dispatcher_skips_poison_messages_past_the_attempt_cap()
    {
        await using var db = NewDb();
        var message = OutboxMessage.Create(SampleEvent(), DateTime.UtcNow);
        for (var i = 0; i < 10; i++)
            message.RecordFailure("prior failure");
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IPublisher>();
        var dispatcher = new OutboxDispatcher(db, publisher, NullLogger<OutboxDispatcher>.Instance);

        var dispatched = await dispatcher.ProcessBatchAsync(batchSize: 50, maxAttempts: 10, CancellationToken.None);

        Assert.Equal(0, dispatched);
        await publisher.DidNotReceive().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeProcessedAsync_removes_only_old_processed_messages()
    {
        await using var db = NewDb();

        var oldProcessed = OutboxMessage.Create(SampleEvent(), DateTime.UtcNow.AddDays(-30));
        oldProcessed.MarkProcessed(DateTime.UtcNow.AddDays(-30)); // processed long ago → purged
        var recentProcessed = OutboxMessage.Create(SampleEvent(), DateTime.UtcNow);
        recentProcessed.MarkProcessed(DateTime.UtcNow); // processed just now → kept
        var pending = OutboxMessage.Create(SampleEvent(), DateTime.UtcNow); // never processed → kept

        db.OutboxMessages.AddRange(oldProcessed, recentProcessed, pending);
        await db.SaveChangesAsync();

        var purged = await dispatcherFor(db).PurgeProcessedAsync(DateTime.UtcNow.AddDays(-7), CancellationToken.None);

        Assert.Equal(1, purged);
        var remaining = await db.OutboxMessages.Select(m => m.Id).ToListAsync();
        Assert.DoesNotContain(oldProcessed.Id, remaining);
        Assert.Contains(recentProcessed.Id, remaining);
        Assert.Contains(pending.Id, remaining);
    }

    [Fact]
    public async Task CountDeadLetteredAsync_counts_unprocessed_messages_past_the_cap()
    {
        await using var db = NewDb();

        var poison = OutboxMessage.Create(SampleEvent(), DateTime.UtcNow);
        for (var i = 0; i < 10; i++)
            poison.RecordFailure("boom"); // Attempts = 10, never processed → dead-lettered
        var healthyPending = OutboxMessage.Create(SampleEvent(), DateTime.UtcNow); // Attempts 0 → not counted

        db.OutboxMessages.AddRange(poison, healthyPending);
        await db.SaveChangesAsync();

        var deadLettered = await dispatcherFor(db).CountDeadLetteredAsync(maxAttempts: 10, CancellationToken.None);

        Assert.Equal(1, deadLettered);
    }

    private static OutboxDispatcher dispatcherFor(AppDbContext db) =>
        new(db, Substitute.For<IPublisher>(), NullLogger<OutboxDispatcher>.Instance);

    /// <summary>Minimal context services for an HTTP-less test: admin so EF global filters never exclude rows.</summary>
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
