using BuildingBlocks.Shared.Abstractions;
using MediatR;

namespace BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;

public interface IDbContextServices
{
    ICurrentUser CurrentUser { get; }
    ITenantContext TenantContext { get; }
    IPublisher Publisher { get; }
}
