namespace Modules.IdentityModule.Infrastructure.Services;

/// <summary>
/// Shared cache contract for the per-request SecurityStamp revocation check (Tier 2).
/// The JWT carries a <c>stamp</c> claim; every request compares it against the user's current
/// stamp. To avoid a DB hit per request the stamp is cached for <see cref="Duration"/>; rotating
/// the stamp (logout-all, password change) must evict the key so revocation takes effect at once.
/// </summary>
public static class SecurityStampCache
{
    /// <summary>Cache window. Bounds worst-case revocation latency for a still-cached stamp.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

    public static string KeyFor(string appUserId) => $"sec-stamp:{appUserId}";
}
