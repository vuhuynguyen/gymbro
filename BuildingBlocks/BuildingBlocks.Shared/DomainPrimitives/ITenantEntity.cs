namespace BuildingBlocks.Shared.DomainPrimitives;

public interface ITenantEntity
{
    Guid TenantId { get; }
}