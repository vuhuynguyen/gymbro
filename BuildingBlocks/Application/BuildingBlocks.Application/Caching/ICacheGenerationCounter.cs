namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Monotonic generation counter for cache-bust invalidation across instances. Each cached entry is
/// keyed with the current generation; bumping the counter makes every entry under that logical key
/// fall through to its factory (old entries expire via their own TTL).
/// </summary>
public interface ICacheGenerationCounter
{
    Task<long> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default);
}
