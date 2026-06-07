using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using MediatR;

namespace BuildingBlocks.Infrastructure.Persistence.Services;

public class DbContextServices(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IPublisher publisher) : IDbContextServices
{
    public ICurrentUser CurrentUser { get; } = currentUser;
    public ITenantContext TenantContext { get; } = tenantContext;
    public IPublisher Publisher { get; } = publisher;
}
