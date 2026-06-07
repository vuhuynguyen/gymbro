namespace BuildingBlocks.Shared.DomainPrimitives;

public interface ISharedEntity
{
    Guid? TenantId { get; }
}