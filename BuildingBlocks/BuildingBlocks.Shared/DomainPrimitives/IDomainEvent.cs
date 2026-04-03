namespace BuildingBlocks.Shared.DomainPrimitives;

public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}