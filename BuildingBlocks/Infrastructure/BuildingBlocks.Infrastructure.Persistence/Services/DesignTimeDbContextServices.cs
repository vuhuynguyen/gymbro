using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using MediatR;

namespace BuildingBlocks.Infrastructure.Persistence.Services;

public class DesignTimeDbContextServices : IDbContextServices
{
    public ICurrentUser CurrentUser { get; } = new DesignTimeCurrentUser();
    public ITenantContext TenantContext { get; } = new DesignTimeTenantContext();
    public IPublisher Publisher { get; } = new NoOpPublisher();
}

file sealed class DesignTimeTenantContext : ITenantContext
{
    public Guid? TenantId => null;
}