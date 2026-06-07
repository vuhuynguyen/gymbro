using BuildingBlocks.Shared.Abstractions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.Identity.Models;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser, ITenantContext
{
    public Guid UserId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User?.FindFirst("domainUserId")?.Value;
            return claim != null ? Guid.Parse(claim) : Guid.Empty;
        }
    }

    public bool IsAdmin
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User?.FindFirst("is_admin")?.Value;
            return claim == "true";
        }
    }

    public Guid? TenantId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
                return null;

            // The raw X-Tenant-Id header is NEVER trusted directly. TenantResolutionMiddleware
            // verifies the caller is a member of the requested tenant (or is a platform admin)
            // and only then stores the id here. This closes X-Tenant-Id spoofing and the
            // resulting cross-tenant data leakage through the EF global query filters.
            if (httpContext.Items.TryGetValue(TenantConstants.ValidatedTenantIdItemKey, out var value)
                && value is Guid tenantId)
            {
                return tenantId;
            }

            return null;
        }
    }
}
