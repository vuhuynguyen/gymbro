using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Application.Queries;

namespace Modules.ExerciseModule.Application.Caching;

/// <summary>
/// Exercise catalog distributed cache (single global scope — the catalog is platform-wide, no tenant key).
/// Two invalidation strategies, each matched to whether the key is known at mutation time:
/// <list type="bullet">
/// <item><b>Detail</b> — keyed by exercise id, so a mutation evicts that one key directly.</item>
/// <item><b>Search</b> — keyed by filter-combo × page (not enumerable), so a mutation bumps a generation
/// counter folded into the key; old pages then fall through to the reader and expire on their TTL.</item>
/// </list>
/// </summary>
public sealed class ExerciseCatalogCache(
    IDistributedCache cache,
    ICacheKeyNamespace keyNamespace,
    ICacheGenerationCounter generations,
    ExerciseCatalogSearchReader searchReader,
    ExerciseCatalogDetailReader detailReader)
{
    private const string CatalogScope = "global";
    private const string SearchCategory = "exercise.search";
    private const string DetailCategory = "exercise.detail";

    private static readonly TimeSpan SearchTtl = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DetailNotFoundTtl = TimeSpan.FromSeconds(30);

    public Task InvalidateSearchAsync(CancellationToken cancellationToken = default) =>
        generations.IncrementAsync(SearchGenerationKey(), cancellationToken);

    public Task InvalidateDetailAsync(Guid exerciseId, CancellationToken cancellationToken = default) =>
        cache.RemoveJsonAsync(keyNamespace.Qualify(DetailKey(exerciseId)), DetailCategory, cancellationToken);

    public async Task<List<ExerciseDto>> GetSearchPageAsync(
        SearchExercisesQuery query,
        CancellationToken cancellationToken)
    {
        var generation = await generations.GetAsync(SearchGenerationKey(), cancellationToken);
        var cacheKey = keyNamespace.Qualify(
            CacheKeys.Versioned(SearchKey(query), generation));

        var rows = await cache.GetOrSetJsonAsync(
            cacheKey,
            async ct =>
            {
                var page = await searchReader.LoadPageAsync(query, ct);
                return (
                    page,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = SearchTtl
                    });
            },
            SearchCategory,
            cancellationToken);

        return rows ?? [];
    }

    public async Task<CacheEnvelope<ExerciseDetailDto>?> GetDetailAsync(
        Guid exerciseId,
        CancellationToken cancellationToken)
    {
        var cacheKey = keyNamespace.Qualify(DetailKey(exerciseId));

        return await cache.GetOrSetJsonAsync(
            cacheKey,
            async ct =>
            {
                var detail = await detailReader.LoadAsync(exerciseId, ct);
                if (detail is null)
                {
                    return (
                        new CacheEnvelope<ExerciseDetailDto> { Exists = false },
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = DetailNotFoundTtl
                        });
                }

                return (
                    new CacheEnvelope<ExerciseDetailDto> { Exists = true, Value = detail },
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = DetailTtl
                    });
            },
            DetailCategory,
            cancellationToken);
    }

    private static string SearchKey(SearchExercisesQuery query) =>
        CacheKeys.WithVersion(
            $"exercise:search:{CatalogScope}:{query.Search ?? ""}:{query.MuscleGroup ?? ""}:{query.Type ?? ""}:{query.MovementType ?? ""}:{query.Difficulty ?? ""}:{query.Equipment ?? ""}:{query.Page}:{query.PageSize}");

    private static string DetailKey(Guid exerciseId) =>
        CacheKeys.WithVersion($"exercise:detail:{exerciseId:N}:{CatalogScope}");

    private static string SearchGenerationKey() => CacheKeys.WithVersion("exercise:search:gen");
}
