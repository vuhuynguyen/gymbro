namespace BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// In-process wake signal between outbox WRITERS (any <c>AppDbContext.SaveChangesAsync</c> that commits
/// domain events) and the <c>OutboxProcessor</c> that drains them.
///
/// <para>
/// This replaces fixed-interval polling. Instead of querying the database every few seconds regardless of
/// activity, the processor blocks on <see cref="WaitAsync"/> with NO database access until a write calls
/// <see cref="Notify"/> — so an idle app leaves the database completely untouched and a serverless Postgres
/// (e.g. Neon) can auto-suspend / scale to zero. A slow fallback interval still forces a periodic safety-net
/// drain (crash recovery, a missed signal, or events written by another instance).
/// </para>
///
/// <para>
/// Signals are COALESCED: many <see cref="Notify"/> calls collapse into a single pending wake, so a burst of
/// writes costs the processor at most one extra drain. Delivery is never lost for a committed message —
/// <see cref="Notify"/> is only called once the message is durably committed, and a signal that arrives while
/// the processor is mid-drain is retained so it loops again.
/// </para>
/// </summary>
public interface IOutboxSignal
{
    /// <summary>
    /// Wakes the processor because a write has committed outbox message(s). Non-blocking and thread-safe:
    /// safe to call from any DI scope or background thread, and a no-op if a wake is already pending.
    /// </summary>
    void Notify();

    /// <summary>
    /// Completes when <see cref="Notify"/> has been called (consuming one pending wake) or when
    /// <paramref name="fallbackInterval"/> elapses with no signal. Throws <see cref="OperationCanceledException"/>
    /// if <paramref name="cancellationToken"/> is cancelled (host shutdown).
    /// </summary>
    Task WaitAsync(TimeSpan fallbackInterval, CancellationToken cancellationToken);
}
