using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Modules.UserModule.Application.DTOs;
using Modules.UserModule.Application.Mapping;
using Modules.UserModule.Application.Queries;
using Modules.UserModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.UserModule.Application.Queries.Handlers;

public class GetTenantMembersHandler(
    ITenantAuthorizationService tenantAuth,
    IUserTenantRoleRepository roleRepository,
    IUserRepository userRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetTenantMembersQuery, Result<List<MemberDto>>>
{
    public async Task<Result<List<MemberDto>>> Handle(GetTenantMembersQuery request, CancellationToken cancellationToken)
    {
        if (!await tenantAuth.HasPermissionAsync(request.TenantId, Permission.ClientView, cancellationToken))
            return Result<List<MemberDto>>.Failure(Forbidden("Forbidden", "You are not a member of this tenant."));

        if (currentUser.UserId == Guid.Empty)
            return Result<List<MemberDto>>.Failure(Unauthorized("Unauthorized", "User context is missing."));

        var roles = (await roleRepository.GetByTenantAsync(request.TenantId, cancellationToken)).ToList();

        var callerMembership =
            await roleRepository.GetByUserAndTenantAsync(currentUser.UserId, request.TenantId, cancellationToken);
        if (callerMembership?.Role == TenantRole.Client)
        {
            roles = roles
                .Where(r => r.UserId == currentUser.UserId || r.Role == TenantRole.Owner)
                .ToList();
        }

        var userIds = roles.Select(r => r.UserId).ToList();

        var users = await userRepository.Query()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        // Include every membership row. Missing User rows still appear (e.g. data drift) so counts match /api/tenants/mine memberCount.
        var result = roles
            .Select(r => UserMapping.ToMemberDto(
                r,
                users.TryGetValue(r.UserId, out var u) ? u.Name : "Unknown member"))
            .ToList();

        return Result<List<MemberDto>>.Success(result);
    }
}
