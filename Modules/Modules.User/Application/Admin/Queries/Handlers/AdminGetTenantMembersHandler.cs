using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.DTOs;
using Modules.UserModule.Application.Mapping;

namespace Modules.UserModule.Application.Admin.Queries.Handlers;

public class AdminGetTenantMembersHandler(
    IUserTenantRoleRepository roleRepository,
    IUserRepository userRepository)
    : IRequestHandler<AdminGetTenantMembersQuery, Result<List<MemberDto>>>
{
    public async Task<Result<List<MemberDto>>> Handle(
        AdminGetTenantMembersQuery request, CancellationToken cancellationToken)
    {
        var roles = await roleRepository.GetByTenantAsync(request.TenantId, cancellationToken);

        if (roles.Count == 0)
            return Result<List<MemberDto>>.Failure(Error.NotFound("Tenant not found or has no members."));

        // Bound the result set with a clamped page/pageSize; defaults preserve the bare-list response shape.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 200);

        var pagedRoles = roles
            .OrderBy(r => r.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var userIds = pagedRoles.Select(r => r.UserId).ToList();

        var users = await userRepository.Query()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var result = pagedRoles
            .Select(r => UserMapping.ToMemberDto(
                r,
                users.TryGetValue(r.UserId, out var u) ? u.Name : "Unknown member"))
            .ToList();

        return Result<List<MemberDto>>.Success(result);
    }
}
