using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

public interface IPermissionService
{
    bool HasPermission(TenantRole role, Permission permission);
}
