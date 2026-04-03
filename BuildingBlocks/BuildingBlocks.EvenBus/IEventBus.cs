namespace BuildingBlocks.EvenBus;

/// <summary>
/// Out-of-process integration (e.g. message broker, eventual consistency between services).
/// Not wired yet — register an implementation when you add a bus. Use MediatR for in-process domain and application events.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(
        IntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}