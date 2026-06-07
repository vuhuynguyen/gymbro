namespace BuildingBlocks.Shared.Abstractions;

public static class TenantConstants
{
    /// <summary>
    /// HttpContext.Items key under which the request's tenant id is stored
    /// ONLY after membership has been verified by the tenant-resolution middleware.
    /// The raw X-Tenant-Id header is never trusted directly.
    /// </summary>
    public const string ValidatedTenantIdItemKey = "ValidatedTenantId";
}
