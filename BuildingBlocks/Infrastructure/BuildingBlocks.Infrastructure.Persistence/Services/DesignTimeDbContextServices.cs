using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;

namespace BuildingBlocks.Infrastructure.Persistence.Services;

public class DesignTimeDbContextServices : IDbContextServices
{
    public ICurrentUser CurrentUser { get; } = new DesignTimeCurrentUser();
    public ITenantContext TenantContext { get; } = new DesignTimeTenantContext();
}

file sealed class DesignTimeTenantContext : ITenantContext
{
    public Guid? TenantId => null;
}
