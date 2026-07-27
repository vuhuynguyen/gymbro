namespace BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>Tunables for the outbox processor, bound from the "Outbox" configuration section.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// Safety-net interval: the longest the processor waits before force-draining the outbox even with no
    /// write signal, to recover events left by a crash, a missed in-process signal, or another instance.
    /// Normal dispatch is signal-driven and immediate (see <see cref="IOutboxSignal"/>) and a startup drain
    /// catches anything left by a restart, so this is deliberately long — the database stays idle between
    /// writes, letting a serverless Postgres scale to zero instead of a short poll pinning it awake.
    /// </summary>
    public TimeSpan FallbackInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Max messages dispatched per poll.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>A message is dead-lettered (left for inspection, no longer retried) after this many failed
    /// attempts, so a poison message can't be retried forever.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Processed messages older than this are purged so the table doesn't grow unbounded.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Master switch for the retention purge loop. Set false to skip it entirely (e.g. a personal
    /// deployment where the outbox volume is negligible and one fewer periodic query helps the DB stay idle).</summary>
    public bool CleanupEnabled { get; set; } = true;

    /// <summary>How often the retention purge runs. Only cosmetic table hygiene, so it runs rarely (daily)
    /// rather than hourly — a purge is only ever needed after real write activity has produced processed rows.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>The outbox health check reports Degraded once dead-lettered messages reach this count.</summary>
    public int DeadLetterAlertThreshold { get; set; } = 1;
}
