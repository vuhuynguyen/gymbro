using BuildingBlocks.Application.Caching;
using BuildingBlocks.Shared.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Queries;

namespace Modules.FoodModule.Application.Caching;

/// <summary>
/// Food catalog distributed cache. Unlike the exercise catalog (platform-wide, single global scope), the
/// food catalog is tenant-aware: a search/detail runs under the EF <c>ISharedEntity</c> filter and returns
/// global rows plus the active tenant's custom rows (admin bypasses the filter and sees everything), so
/// every key carries a <b>scope</b> segment (<c>admin</c> or <c>t{tenantId}</c>) derived from the caller.
/// Two invalidation strategies, both generation-counter based (mirroring the exercise search pattern):
/// <list type="bullet">
/// <item><b>Search</b> — keyed by scope × filter-combo × page (not enumerable), so a mutation bumps a single
/// generation counter folded into the key; old pages then fall through to the reader and expire on TTL.</item>
/// <item><b>Detail</b> — keyed by scope × food id. The scopes a food is cached under are not enumerable at
/// mutation time (a global food is cached per tenant), so invalidation bumps a per-food generation counter
/// rather than removing keys directly (the only intentional divergence from <c>ExerciseCatalogCache</c>).</item>
/// </list>
/// </summary>
public sealed class FoodCatalogCache(
    IDistributedCache cache,
    ICacheKeyNamespace keyNamespace,
    ICacheGenerationCounter generations,
    FoodCatalogSearchReader searchReader,
    FoodCatalogDetailReader detailReader,
    ICurrentUser currentUser,
    ITenantContext tenantContext)
{
    private const string SearchCategory = "food.search";
    private const string DetailCategory = "food.detail";

    private static readonly TimeSpan SearchTtl = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DetailNotFoundTtl = TimeSpan.FromSeconds(30);

    public Task InvalidateSearchAsync(CancellationToken cancellationToken = default) =>
        generations.IncrementAsync(SearchGenerationKey(), cancellationToken);

    public Task InvalidateDetailAsync(Guid foodId, CancellationToken cancellationToken = default) =>
        generations.IncrementAsync(DetailGenerationKey(foodId), cancellationToken);

    public async Task<FoodListDto> GetSearchPageAsync(
        SearchFoodsQuery query,
        CancellationToken cancellationToken)
    {
        var generation = await generations.GetAsync(SearchGenerationKey(), cancellationToken);
        var cacheKey = keyNamespace.Qualify(
            CacheKeys.Versioned(SearchKey(query, CallerScope()), generation));

        var page = await cache.GetOrSetJsonAsync(
            cacheKey,
            async ct =>
            {
                var result = await searchReader.LoadPageAsync(query, ct);
                return (
                    result,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = SearchTtl
                    });
            },
            SearchCategory,
            cancellationToken);

        return page ?? await searchReader.LoadPageAsync(query, cancellationToken);
    }

    public async Task<CacheEnvelope<FoodDto>?> GetDetailAsync(
        Guid foodId,
        CancellationToken cancellationToken)
    {
        var generation = await generations.GetAsync(DetailGenerationKey(foodId), cancellationToken);
        var cacheKey = keyNamespace.Qualify(
            CacheKeys.Versioned(DetailKey(foodId, CallerScope()), generation));

        return await cache.GetOrSetJsonAsync(
            cacheKey,
            async ct =>
            {
                var detail = await detailReader.LoadAsync(foodId, ct);
                if (detail is null)
                {
                    return (
                        new CacheEnvelope<FoodDto> { Exists = false },
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = DetailNotFoundTtl
                        });
                }

                return (
                    new CacheEnvelope<FoodDto> { Exists = true, Value = detail },
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = DetailTtl
                    });
            },
            DetailCategory,
            cancellationToken);
    }

    /// <summary>The visibility scope the EF tenant filter gives this caller — folded into every key so one
    /// tenant's rows are never served from another tenant's (or the admin's) cache entry.</summary>
    private string CallerScope() =>
        currentUser.IsAdmin
            ? "admin"
            : tenantContext.TenantId is { } tenantId
                ? $"t{tenantId:N}"
                : "anon";

    private static string SearchKey(SearchFoodsQuery query, string scope)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        return CacheKeys.WithVersion(
            $"food:search:{scope}:{query.Search?.Trim().ToLowerInvariant() ?? ""}:{query.Kind?.Trim().ToLowerInvariant() ?? ""}:{page}:{pageSize}");
    }

    private static string DetailKey(Guid foodId, string scope) =>
        CacheKeys.WithVersion($"food:detail:{foodId:N}:{scope}");

    private static string SearchGenerationKey() => CacheKeys.WithVersion("food:search:gen");

    private static string DetailGenerationKey(Guid foodId) =>
        CacheKeys.WithVersion($"food:detail:{foodId:N}:gen");
}
