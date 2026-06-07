using System.Linq.Expressions;
using Modules.UserModule.Application.DTOs;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Mapping;

internal static class UserMapping
{
    public static Expression<Func<User, AdminUserDto>> AdminUserProjection =>
        u => new AdminUserDto
        {
            Id = u.Id,
            Name = u.Name,
            CreatedOnUtc = u.CreatedOnUtc
        };

    public static TenantDto ToTenantDto(
        UserTenantRole role,
        Tenant tenant,
        int memberCount,
        string? ownerName) =>
        new()
        {
            Id = role.TenantId!.Value,
            Name = tenant.Name,
            Role = role.Role.ToString(),
            JoinedAt = role.CreatedOnUtc,
            MemberCount = memberCount,
            OwnerName = ownerName
        };

    public static MemberDto ToMemberDto(UserTenantRole role, string name) =>
        new()
        {
            UserId = role.UserId,
            Name = name,
            Role = role.Role.ToString(),
            JoinedAt = role.CreatedOnUtc
        };

    public static InviteCodeDto ToInviteCodeDto(Invite invite, DateTimeOffset now) =>
        new()
        {
            Code = invite.Code,
            CreatedAt = invite.CreatedOnUtc,
            ExpiresAt = invite.ExpiredAt,
            IsUsed = invite.IsUsed,
            IsExpired = !invite.IsUsed && invite.ExpiredAt <= now
        };

    public static AdminTenantDto ToAdminTenantDto(
        Tenant tenant,
        string ownerName,
        int memberCount) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            OwnerUserId = tenant.OwnerUserId,
            OwnerName = ownerName,
            MemberCount = memberCount,
            CreatedOnUtc = tenant.CreatedOnUtc
        };
}
