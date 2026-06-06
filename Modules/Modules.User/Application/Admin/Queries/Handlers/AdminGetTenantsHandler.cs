using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.DTOs;
using Modules.UserModule.Application.Mapping;

namespace Modules.UserModule.Application.Admin.Queries.Handlers;

public class AdminGetTenantsHandler(
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    IUserTenantRoleRepository roleRepository)
    : IRequestHandler<AdminGetTenantsQuery, Result<List<AdminTenantDto>>>
{
    public async Task<Result<List<AdminTenantDto>>> Handle(
        AdminGetTenantsQuery request, CancellationToken cancellationToken)
    {
        // Bound the result set with a clamped page/pageSize; defaults preserve the bare-list response shape.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 200);

        var tenants = await tenantRepository.Query()
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ownerIds = tenants.Select(t => t.OwnerUserId).Distinct().ToList();
        var ownerMap = await userRepository.Query()
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var allRoles = await roleRepository.Query()
            .AsNoTracking()
            .Where(r => tenants.Select(t => t.Id).Contains(r.TenantId!.Value))
            .GroupBy(r => r.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = allRoles.ToDictionary(x => x.TenantId, x => x.Count);

        var result = tenants
            .Select(t => UserMapping.ToAdminTenantDto(
                t,
                ownerMap.TryGetValue(t.OwnerUserId, out var name) ? name : "Unknown",
                countMap.TryGetValue(t.Id, out var count) ? count : 0))
            .ToList();

        return Result<List<AdminTenantDto>>.Success(result);
    }
}
