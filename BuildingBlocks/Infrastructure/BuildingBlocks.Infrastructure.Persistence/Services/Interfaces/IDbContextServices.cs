using BuildingBlocks.Shared.Abstractions;

namespace BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;

public interface IDbContextServices
{
    ICurrentUser CurrentUser { get; }
    ITenantContext TenantContext { get; }
}
