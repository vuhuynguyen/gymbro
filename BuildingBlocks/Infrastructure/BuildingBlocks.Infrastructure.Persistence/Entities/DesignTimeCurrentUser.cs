using BuildingBlocks.Shared.Abstractions;

namespace BuildingBlocks.Infrastructure.Persistence;

public class DesignTimeCurrentUser : ICurrentUser
{
    public Guid UserId => Guid.Empty; // or any fixed value
    public Guid? TenantId => null; // allow global data
    public bool IsAdmin => true;   // bypass filters
    public string? TimeZoneId => null;
}