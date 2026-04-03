using BuildingBlocks.Shared.Abstractions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.Identity.Models;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    //
    // public Guid UserId =>
    //     Guid.Parse(GetClaim("sub"));
    //
    // public Guid TenantId =>
    //     Guid.Parse(GetClaim("tenant_id"));
    //
    // public bool IsAdmin =>
    //     bool.Parse(GetClaim("is_admin"));

    public Guid UserId { get; }
    Guid? ICurrentUser.TenantId => TenantId;

    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User?
                .FindFirst("tenant_id")?.Value;

            return claim != null ? Guid.Parse(claim) : Guid.Empty;
        }
    }

    public bool IsAdmin
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User?
                .FindFirst("role")?.Value;

            return claim == "admin";
        }
    }

    private string GetClaim(string name)
    {
        return _httpContextAccessor
                   .HttpContext?
                   .User?
                   .FindFirst(name)?.Value
               ?? throw new Exception($"Claim '{name}' not found");
    }
}