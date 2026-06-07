using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BuildingBlocks.Application.Caching;

public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Ref-counted gates: a key's gate stays registered while any caller still holds a reference, so a
    // waiting caller can never be stranded on a gate that another caller has already removed. (The earlier
    // "remove when CurrentCount == 1" approach raced: a fresh caller could create a second gate for the
    // same key while a waiter still held the first, silently breaking the single-flight collapse.)
    private sealed class RefCountedGate
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int Refs;
    }

    private static readonly Dictionary<string, RefCountedGate> Gates = new();

    public static async Task<T?> GetJsonAsync<T>(
        this IDistributedCache cache,
        string key,
        string category = "default",
        CancellationToken cancellationToken = default)
    {
        var value = await ReadJsonAsync<T>(cache, key, cancellationToken);
        if (value is null)
            CacheTelemetry.RecordMiss(category);
        else
            CacheTelemetry.RecordHit(category);

        return value;
    }

    public static Task SetJsonAsync<T>(
        this IDistributedCache cache,
        string key,
        T value,
        DistributedCacheEntryOptions options,
        string category = "default",
        CancellationToken cancellationToken = default)
    {
        CacheTelemetry.RecordSet(category);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return cache.SetAsync(key, bytes, options, cancellationToken);
    }

    /// <summary>
    /// Best-effort cache eviction: invalidation must never fail a write that already committed, so a cache
    /// fault (e.g. Redis down) is swallowed — the stale entry expires on its own TTL.
    /// </summary>
    public static async Task RemoveJsonAsync(
        this IDistributedCache cache,
        string key,
        string category = "default",
        CancellationToken cancellationToken = default)
    {
        CacheTelemetry.RecordRemove(category);
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CacheTelemetry.RecordError(category);
        }
    }

    /// <summary>
    /// Cache-aside with single-flight collapse of concurrent misses. The cache is a performance layer, not
    /// a source of truth: if it faults (Redis unavailable) the call degrades to <paramref name="factory"/>
    /// (the DB) instead of failing — reads stay correct and available, just slower.
    /// </summary>
    public static async Task<T?> GetOrSetJsonAsync<T>(
        this IDistributedCache cache,
        string key,
        Func<CancellationToken, Task<(T? Value, DistributedCacheEntryOptions Options)>> factory,
        string category = "default",
        CancellationToken cancellationToken = default)
    {
        var (hit, cached) = await TryReadJsonAsync<T>(cache, key, category, cancellationToken);
        if (hit)
        {
            CacheTelemetry.RecordHit(category);
            return cached;
        }

        // Defer the hit/miss record until inside the gate: a concurrent caller may have filled the entry
        // while we waited, in which case this call is a hit, not a miss — recording both would skew ratios.
        return await WithSingleFlightAsync(
            key,
            async () =>
            {
                var (rechit, rechecked) = await TryReadJsonAsync<T>(cache, key, category, cancellationToken);
                if (rechit)
                {
                    CacheTelemetry.RecordHit(category);
                    return rechecked;
                }

                CacheTelemetry.RecordMiss(category);
                var (value, options) = await factory(cancellationToken);
                if (value is not null)
                    await TrySetJsonAsync(cache, key, value, options, category, cancellationToken);

                return value;
            },
            cancellationToken);
    }

    public static async Task<string?> GetOrSetStringAsync(
        this IDistributedCache cache,
        string key,
        Func<CancellationToken, Task<(string? Value, DistributedCacheEntryOptions Options)>> factory,
        string category = "default",
        CancellationToken cancellationToken = default)
    {
        var (hit, cached) = await TryReadStringAsync(cache, key, category, cancellationToken);
        if (hit)
        {
            CacheTelemetry.RecordHit(category);
            return cached;
        }

        // Defer the hit/miss record until inside the gate (see GetOrSetJsonAsync).
        return await WithSingleFlightAsync(
            key,
            async () =>
            {
                var (rechit, rechecked) = await TryReadStringAsync(cache, key, category, cancellationToken);
                if (rechit)
                {
                    CacheTelemetry.RecordHit(category);
                    return rechecked;
                }

                CacheTelemetry.RecordMiss(category);
                var (value, options) = await factory(cancellationToken);
                if (value is not null)
                {
                    try
                    {
                        CacheTelemetry.RecordSet(category);
                        await cache.SetStringAsync(key, value, options, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        CacheTelemetry.RecordError(category);   // best-effort write — value is already loaded
                    }
                }

                return value;
            },
            cancellationToken);
    }

    private static async Task<T?> ReadJsonAsync<T>(
        IDistributedCache cache,
        string key,
        CancellationToken cancellationToken)
    {
        var bytes = await cache.GetAsync(key, cancellationToken);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    // Returns (hit, value). A cache fault is reported and treated as a miss so the caller falls through to
    // the source of truth rather than propagating the exception.
    private static async Task<(bool Hit, T? Value)> TryReadJsonAsync<T>(
        IDistributedCache cache,
        string key,
        string category,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await ReadJsonAsync<T>(cache, key, cancellationToken);
            return (value is not null, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CacheTelemetry.RecordError(category);
            return (false, default);
        }
    }

    private static async Task<(bool Hit, string? Value)> TryReadStringAsync(
        IDistributedCache cache,
        string key,
        string category,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await cache.GetStringAsync(key, cancellationToken);
            return (value is not null, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CacheTelemetry.RecordError(category);
            return (false, null);
        }
    }

    private static async Task TrySetJsonAsync<T>(
        IDistributedCache cache,
        string key,
        T value,
        DistributedCacheEntryOptions options,
        string category,
        CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetJsonAsync(key, value, options, category, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CacheTelemetry.RecordError(category);   // best-effort write — value is already loaded
        }
    }

    private static async Task<T> WithSingleFlightAsync<T>(
        string key,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        RefCountedGate gate;
        lock (Gates)
        {
            if (!Gates.TryGetValue(key, out gate!))
                Gates[key] = gate = new RefCountedGate();
            gate.Refs++;
        }

        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            ReleaseRef(key, gate);   // never acquired the semaphore — just drop our reference
            throw;
        }

        try
        {
            return await action();
        }
        finally
        {
            gate.Semaphore.Release();
            ReleaseRef(key, gate);
        }
    }

    private static void ReleaseRef(string key, RefCountedGate gate)
    {
        lock (Gates)
        {
            if (--gate.Refs == 0)
                Gates.Remove(key);
        }
    }
}
