using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Queries.Handlers;

/// <summary>
/// Resolves the Client members of a tenant + their display names for the coach roster. Internal lookup:
/// no permission gate of its own — the caller (the coach roster handler) has already verified
/// <c>WorkoutLogViewAll</c> on the active tenant, and the tenant id passed here is that already-authorized
/// active tenant, never a client-supplied one. Only <see cref="TenantRole.Client"/> rows are returned, and a
/// membership whose <c>User</c> row is missing (data drift) degrades to "Unknown member" so counts stay stable.
/// </summary>
public sealed class ResolveTenantMemberNamesHandler(
    IUserTenantRoleRepository roleRepository,
    IUserRepository userRepository)
    : IRequestHandler<ResolveTenantMemberNamesQuery, Result<IReadOnlyList<TenantMemberNameDto>>>
{
    public async Task<Result<IReadOnlyList<TenantMemberNameDto>>> Handle(
        ResolveTenantMemberNamesQuery request,
        CancellationToken cancellationToken)
    {
        var clientRoles = (await roleRepository.GetByTenantAsync(request.TenantId, cancellationToken))
            .Where(r => r.Role == TenantRole.Client)
            .ToList();

        if (clientRoles.Count == 0)
            return Result<IReadOnlyList<TenantMemberNameDto>>.Success([]);

        var userIds = clientRoles.Select(r => r.UserId).Distinct().ToList();

        // One read carries both the display name AND the stored zone, so the roster needn't issue a
        // per-member GetUserTimeZoneQuery round-trip (the rows are loaded here either way).
        var users = await userRepository.Query()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Name, u.TimeZoneId }, cancellationToken);

        var members = clientRoles
            .Select(r => users.TryGetValue(r.UserId, out var u)
                ? new TenantMemberNameDto(r.UserId, u.Name, u.TimeZoneId)
                : new TenantMemberNameDto(r.UserId, "Unknown member", null))
            .ToList();

        return Result<IReadOnlyList<TenantMemberNameDto>>.Success(members);
    }
}
