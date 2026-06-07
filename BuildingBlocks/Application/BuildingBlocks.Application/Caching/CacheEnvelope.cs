namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Wraps a cached value so a missing entity (404) can be stored without colliding with a cache miss.
/// </summary>
public sealed class CacheEnvelope<T>
{
    public required bool Exists { get; init; }

    public T? Value { get; init; }
}
