namespace BuildingBlocks.Shared.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? TenantId { get; }
    bool IsAdmin { get; }  
}