using BuildingBlocks.Shared.Abstractions;
using Modules.UserModule.Application.Abstractions;

namespace WebApi.Middleware;

/// <summary>
/// Resolves the active tenant for the request from the X-Tenant-Id header, but only
/// after verifying the authenticated caller is actually a member of that tenant (platform
/// admins bypass the membership check, consistent with the EF global-filter admin bypass).
///
/// The verified tenant id is stashed in <see cref="HttpContext.Items"/> under
/// <see cref="TenantConstants.ValidatedTenantIdItemKey"/>; <c>CurrentUser.TenantId</c> reads
/// only from there. A spoofed header naming a tenant the caller does not belong to is simply
/// ignored, so tenant-scoped EF queries resolve to no tenant (and return nothing) and
/// per-handler permission checks fail — closing both X-Tenant-Id spoofing and the resulting
/// cross-tenant data leakage.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string TenantHeader = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var headerValue = context.Request.Headers[TenantHeader].FirstOrDefault();

        if (Guid.TryParse(headerValue, out var tenantId)
            && tenantId != Guid.Empty
            && context.User.Identity?.IsAuthenticated == true)
        {
            if (IsPlatformAdmin(context))
            {
                context.Items[TenantConstants.ValidatedTenantIdItemKey] = tenantId;
            }
            else if (TryGetDomainUserId(context, out var userId))
            {
                var roleRepository = context.RequestServices.GetRequiredService<IUserTenantRoleRepository>();
                var membership = await roleRepository.GetByUserAndTenantAsync(
                    userId, tenantId, context.RequestAborted);

                if (membership is not null)
                    context.Items[TenantConstants.ValidatedTenantIdItemKey] = tenantId;
            }
        }

        await next(context);
    }

    private static bool IsPlatformAdmin(HttpContext context) =>
        context.User.FindFirst("is_admin")?.Value == "true";

    private static bool TryGetDomainUserId(HttpContext context, out Guid userId)
    {
        var claim = context.User.FindFirst("domainUserId")?.Value;
        return Guid.TryParse(claim, out userId) && userId != Guid.Empty;
    }
}
