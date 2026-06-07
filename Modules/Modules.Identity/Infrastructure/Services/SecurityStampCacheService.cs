using BuildingBlocks.Application.Caching;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Modules.IdentityModule.Application.Abstractions;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Infrastructure.Services;

/// <summary>
/// Tier-2 JWT revocation check. Owns its cache key: a present entry means the (briefly cached)
/// current SecurityStamp; rotating the stamp (logout-all, password change/reset, user delete) must
/// <see cref="EvictAsync"/> the key so revocation takes effect at once rather than after the TTL.
/// </summary>
public sealed class SecurityStampCacheService(
    IDistributedCache cache,
    ICacheKeyNamespace keyNamespace,
    UserManager<AppUser> userManager) : ISecurityStampCacheService
{
    private const string CacheCategory = "identity.stamp";

    private static string LogicalKey(string appUserId) =>
        CacheKeys.WithVersion($"identity:sec-stamp:{appUserId}");

    public async Task<bool> IsStampValidAsync(
        string appUserId,
        string tokenStamp,
        CancellationToken cancellationToken = default)
    {
        var stampKey = keyNamespace.Qualify(LogicalKey(appUserId));

        var currentStamp = await cache.GetOrSetStringAsync(
            stampKey,
            async ct =>
            {
                var user = await userManager.FindByIdAsync(appUserId);
                if (user is null)
                    return (null, new DistributedCacheEntryOptions());

                var stamp = await userManager.GetSecurityStampAsync(user);
                return (
                    stamp,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CachePolicies.SecurityStamp
                    });
            },
            CacheCategory,
            cancellationToken);

        return currentStamp is not null
               && string.Equals(currentStamp, tokenStamp, StringComparison.Ordinal);
    }

    public Task EvictAsync(string appUserId, CancellationToken cancellationToken = default)
    {
        var stampKey = keyNamespace.Qualify(LogicalKey(appUserId));
        return cache.RemoveJsonAsync(stampKey, CacheCategory, cancellationToken);
    }
}
