using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.DTOs;
using Modules.UserModule.Application.Mapping;
using Modules.UserModule.Application.Queries;

namespace Modules.UserModule.Application.Queries.Handlers;

public class GetMyTenantsHandler(
    IUserTenantRoleRepository roleRepository,
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyTenantsQuery, Result<List<TenantDto>>>
{
    public async Task<Result<List<TenantDto>>> Handle(GetMyTenantsQuery request, CancellationToken cancellationToken)
    {
        var roles = await roleRepository.GetByUserAsync(currentUser.UserId, cancellationToken);

        if (roles.Count == 0)
            return Result<List<TenantDto>>.Success([]);

        var tenantIds = roles.Select(r => r.TenantId!.Value).ToList();

        var tenants = await tenantRepository.Query()
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id) && !t.IsDeleted)
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        // member counts for owned tenants
        var allRolesForTenants = await roleRepository.Query()
            .AsNoTracking()
            .Where(r => tenantIds.Contains(r.TenantId!.Value))
            .ToListAsync(cancellationToken);

        var memberCounts = allRolesForTenants
            .GroupBy(r => r.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // owner names for tenants the user joined as Client
        var ownerUserIds = tenants.Values.Select(t => t.OwnerUserId).Distinct().ToList();
        var ownerUsers = await userRepository.Query()
            .AsNoTracking()
            .Where(u => ownerUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var result = roles
            .Where(r => tenants.ContainsKey(r.TenantId!.Value))
            .Select(r =>
            {
                var tenant = tenants[r.TenantId!.Value];
                return UserMapping.ToTenantDto(
                    r,
                    tenant,
                    memberCounts.GetValueOrDefault(r.TenantId!.Value, 1),
                    ownerUsers.GetValueOrDefault(tenant.OwnerUserId));
            })
            .ToList();

        return Result<List<TenantDto>>.Success(result);
    }
}
