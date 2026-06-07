using BuildingBlocks.Shared.DomainPrimitives;

namespace BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// A persisted domain event awaiting out-of-band dispatch. Written inside the SAME transaction as the
/// state change that raised it (<c>AppDbContext.SaveChangesAsync</c>), then published by the
/// <c>OutboxProcessor</c>. Delivery is <b>at-least-once</b>: a message is only marked processed after its
/// publish returns, and a failed message stays pending (up to <c>MaxAttempts</c>) for retry — so domain
/// event handlers must be idempotent.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
        // EF materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>When the originating change was saved (UTC).</summary>
    public DateTime OccurredOnUtc { get; private set; }

    /// <summary>Assembly-qualified name of the domain event, used to rehydrate it for dispatch.</summary>
    public string Type { get; private set; } = null!;

    /// <summary>JSON-serialized event payload.</summary>
    public string Content { get; private set; } = null!;

    /// <summary>Null until the event has been dispatched successfully.</summary>
    public DateTime? ProcessedOnUtc { get; private set; }

    /// <summary>Number of dispatch attempts (success or failure).</summary>
    public int Attempts { get; private set; }

    /// <summary>Last dispatch error, if any (cleared on success).</summary>
    public string? Error { get; private set; }

    public static OutboxMessage Create(IDomainEvent domainEvent, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var type = domainEvent.GetType();
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = utcNow,
            Type = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            Content = OutboxSerializer.Serialize(domainEvent, type)
        };
    }

    public void MarkProcessed(DateTime utcNow)
    {
        ProcessedOnUtc = utcNow;
        Attempts += 1;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        Attempts += 1;
        Error = error.Length <= 2000 ? error : error[..2000];
    }
}
