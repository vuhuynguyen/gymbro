using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;

namespace BuildingBlocks.Infrastructure.Persistence.Services;

public class DbContextServices(
    ICurrentUser currentUser,
    ITenantContext tenantContext) : IDbContextServices
{
    public ICurrentUser CurrentUser { get; } = currentUser;
    public ITenantContext TenantContext { get; } = tenantContext;
}
