using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

public static class TenantPermissionMessages
{
    public const string UnauthorizedTemplate =
        "You do not have permission to perform this action in this workspace.";

    public static string GetUnauthorizedMessage(Permission permission) => UnauthorizedTemplate;
}
