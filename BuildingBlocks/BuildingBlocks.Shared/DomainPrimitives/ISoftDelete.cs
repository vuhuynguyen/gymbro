namespace BuildingBlocks.Shared.DomainPrimitives;

public interface ISoftDelete
{
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedOnUtc { get; set; }
}