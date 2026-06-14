using BuildingBlocks.Shared.Abstractions;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Mutable stand-in for the HTTP-scoped <c>CurrentUser</c> (which reads JWT claims + the tenant
/// validated by <c>TenantResolutionMiddleware</c>). Integration tests dispatch MediatR requests with
/// no HTTP pipeline, so they set the caller identity and resolved tenant directly here before sending.
/// Registered as a singleton so the EF global query filters (which capture the context's
/// <see cref="ITenantContext"/>) read the value current at query time.
/// </summary>
public sealed class TestPrincipal : ICurrentUser, ITenantContext
{
    public Guid UserId { get; private set; }
    public bool IsAdmin { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? TimeZoneId { get; set; }

    /// <summary>Impersonate a tenant member for the next dispatch.</summary>
    public void Become(Guid userId, Guid tenantId, bool isAdmin = false)
    {
        UserId = userId;
        TenantId = tenantId;
        IsAdmin = isAdmin;
    }
}
