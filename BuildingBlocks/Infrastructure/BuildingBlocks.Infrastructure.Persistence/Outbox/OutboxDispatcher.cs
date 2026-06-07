using System.Diagnostics.Metrics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Processes one batch of pending outbox messages. Kept separate from the hosted polling loop
/// (<c>OutboxProcessor</c>) so the dispatch logic is directly unit-testable.
///
/// <para>
/// <b>Multi-instance safe.</b> On a relational store the batch is claimed with
/// <c>FOR UPDATE SKIP LOCKED</c> inside a transaction, so two instances polling concurrently never grab
/// the same rows (the row lock is held only for the batch's processing and released on commit). On a
/// non-relational store (InMemory unit tests) it falls back to a plain take — still correct, just without
/// cross-instance claiming.
/// </para>
///
/// <para>
/// Delivery is <b>at-least-once</b>: a message is marked processed only after its publish returns; a publish
/// failure records the error, increments the attempt count, and leaves the message pending for retry until
/// <c>MaxAttempts</c> (after which it is a dead letter — see <see cref="CountDeadLetteredAsync"/>). Handlers
/// must therefore be idempotent.
/// </para>
/// </summary>
public sealed class OutboxDispatcher(
    AppDbContext db,
    IPublisher publisher,
    ILogger<OutboxDispatcher> logger)
{
    private static readonly Meter Meter = new("GymBro.Outbox");
    private static readonly Counter<long> DispatchedCounter = Meter.CreateCounter<long>("outbox.dispatched");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("outbox.failed");

    /// <summary>Claims and dispatches up to <paramref name="batchSize"/> pending messages. Returns the
    /// number successfully published.</summary>
    public async Task<int> ProcessBatchAsync(int batchSize, int maxAttempts, CancellationToken cancellationToken)
    {
        // Relational: claim the batch under a transaction with SKIP LOCKED so concurrent instances don't
        // double-dispatch. Non-relational (tests): no transaction support, so process directly.
        if (!db.Database.IsRelational())
            return await ProcessClaimedBatchAsync(batchSize, maxAttempts, relational: false, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var dispatched = await ProcessClaimedBatchAsync(batchSize, maxAttempts, relational: true, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return dispatched;
    }

    private async Task<int> ProcessClaimedBatchAsync(
        int batchSize,
        int maxAttempts,
        bool relational,
        CancellationToken cancellationToken)
    {
        var messages = relational
            // The row lock claims these messages for this transaction; other workers SKIP LOCKED past them.
            ? await db.Set<OutboxMessage>()
                .FromSqlInterpolated(
                    $@"SELECT * FROM ""OutboxMessages""
                       WHERE ""ProcessedOnUtc"" IS NULL AND ""Attempts"" < {maxAttempts}
                       ORDER BY ""OccurredOnUtc""
                       LIMIT {batchSize}
                       FOR UPDATE SKIP LOCKED")
                .ToListAsync(cancellationToken)
            : await db.Set<OutboxMessage>()
                .Where(m => m.ProcessedOnUtc == null && m.Attempts < maxAttempts)
                .OrderBy(m => m.OccurredOnUtc)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return 0;

        var dispatched = 0;
        foreach (var message in messages)
        {
            try
            {
                var notification = OutboxSerializer.Deserialize(message);
                await publisher.Publish(notification, cancellationToken);
                message.MarkProcessed(DateTime.UtcNow);
                dispatched++;
                DispatchedCounter.Add(1);
            }
            catch (Exception ex)
            {
                message.RecordFailure(ex.Message);
                FailedCounter.Add(1);
                logger.LogError(
                    ex,
                    "Failed to dispatch outbox message {MessageId} ({MessageType}); attempt {Attempts}.",
                    message.Id,
                    message.Type,
                    message.Attempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return dispatched;
    }

    /// <summary>Hard-deletes processed messages whose <c>ProcessedOnUtc</c> is older than
    /// <paramref name="cutoffUtc"/>, so the table does not grow unbounded. Returns the number removed.</summary>
    public async Task<int> PurgeProcessedAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
            return await db.Set<OutboxMessage>()
                .Where(m => m.ProcessedOnUtc != null && m.ProcessedOnUtc < cutoffUtc)
                .ExecuteDeleteAsync(cancellationToken);

        var stale = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc != null && m.ProcessedOnUtc < cutoffUtc)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
            return 0;
        db.Set<OutboxMessage>().RemoveRange(stale);
        await db.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    /// <summary>Counts dead-lettered messages: unprocessed and past the attempt cap (so the poller has given
    /// up on them). Surfaced by the outbox health check so poison messages are visible, not silent.</summary>
    public Task<int> CountDeadLetteredAsync(int maxAttempts, CancellationToken cancellationToken) =>
        db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null && m.Attempts >= maxAttempts)
            .CountAsync(cancellationToken);
}
