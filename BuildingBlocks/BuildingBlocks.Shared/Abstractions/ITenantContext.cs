namespace BuildingBlocks.Shared.Abstractions;

public interface ITenantContext
{
    Guid? TenantId { get; }
}
