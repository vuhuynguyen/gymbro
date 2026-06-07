namespace BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>Tunables for the outbox processor, bound from the "Outbox" configuration section.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the processor polls for pending messages.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Max messages dispatched per poll.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>A message is dead-lettered (left for inspection, no longer retried) after this many failed
    /// attempts, so a poison message can't be retried forever.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Processed messages older than this are purged so the table doesn't grow unbounded.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How often the retention purge runs.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The outbox health check reports Degraded once dead-lettered messages reach this count.</summary>
    public int DeadLetterAlertThreshold { get; set; } = 1;
}
