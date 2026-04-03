namespace BuildingBlocks.Shared.DomainPrimitives;

public interface IAuditable
{
    DateTimeOffset CreatedOnUtc { get; }
    DateTimeOffset? ModifiedOnUtc { get; }
}