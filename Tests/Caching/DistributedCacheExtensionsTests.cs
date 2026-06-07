using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gymbro.Tests.Caching;

public sealed class DistributedCacheExtensionsTests
{
    [Fact]
    public async Task GetOrSetJsonAsync_collapses_concurrent_misses_to_one_factory_call()
    {
        var cache = CreateMemoryCache();
        const string key = "test:single-flight";
        var factoryCalls = 0;

        async Task<List<int>> Load(CancellationToken ct)
        {
            Interlocked.Increment(ref factoryCalls);
            await Task.Delay(50, ct);
            return ([1, 2, 3]);
        }

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => cache.GetOrSetJsonAsync(
                key,
                async ct => (await Load(ct), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                }),
                "test"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, factoryCalls);
        Assert.All(results, rows => Assert.Equal([1, 2, 3], rows));
    }

    [Fact]
    public async Task GetOrSetJsonAsync_caches_negative_envelope()
    {
        var cache = CreateMemoryCache();
        const string key = "test:negative";
        var factoryCalls = 0;

        var first = await cache.GetOrSetJsonAsync(
            key,
            async ct =>
            {
                Interlocked.Increment(ref factoryCalls);
                await Task.CompletedTask;
                return (
                    new CacheEnvelope<string> { Exists = false },
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                    });
            },
            "test",
            CancellationToken.None);

        var second = await cache.GetJsonAsync<CacheEnvelope<string>>(key, "test");

        Assert.NotNull(first);
        Assert.False(first!.Exists);
        Assert.NotNull(second);
        Assert.False(second!.Exists);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrSetJsonAsync_degrades_to_factory_when_cache_faults()
    {
        var cache = new ThrowingCache();
        var factoryCalls = 0;

        var result = await cache.GetOrSetJsonAsync<List<int>>(
            "test:degrade",
            async ct =>
            {
                Interlocked.Increment(ref factoryCalls);
                await Task.CompletedTask;
                return ([7, 8, 9], new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                });
            },
            "test",
            CancellationToken.None);

        // Cache read AND write throw, yet the call returns the source-of-truth value rather than failing.
        Assert.Equal([7, 8, 9], result);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RemoveJsonAsync_swallows_cache_faults()
    {
        var cache = new ThrowingCache();

        // Invalidation must never throw onto an already-committed write.
        await cache.RemoveJsonAsync("test:degrade", "test");
    }

    private static MemoryDistributedCache CreateMemoryCache() =>
        new(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

    /// <summary>Simulates Redis being unavailable: every operation throws.</summary>
    private sealed class ThrowingCache : IDistributedCache
    {
        private static Exception Fault() => new InvalidOperationException("cache unavailable");

        public byte[]? Get(string key) => throw Fault();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Fault();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Fault();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Fault();
        public void Refresh(string key) => throw Fault();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Fault();
        public void Remove(string key) => throw Fault();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Fault();
    }
}
