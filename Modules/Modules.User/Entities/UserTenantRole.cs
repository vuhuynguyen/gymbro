using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.UserModule.Entities;

public class UserTenantRole : AggregateRoot
{
    public Guid UserId { get; private set; }
    public TenantRole Role { get; private set; }

    // TenantId is inherited from BaseEntity (Guid?)

    private UserTenantRole() { }

    public static UserTenantRole Create(Guid userId, Guid tenantId, TenantRole role)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId is required.");

        return new UserTenantRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Role = role
        };
    }
}
