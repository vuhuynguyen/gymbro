namespace BuildingBlocks.EvenBus;

/// <summary>
/// Base type for integration events published through <see cref="IEventBus"/> (cross-boundary, serializable contracts).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}