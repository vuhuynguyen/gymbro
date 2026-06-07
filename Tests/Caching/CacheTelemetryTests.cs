using System.Diagnostics.Metrics;
using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gymbro.Tests.Caching;

public sealed class CacheTelemetryTests
{
    [Fact]
    public async Task GetOrSetJsonAsync_records_hits_and_misses()
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        using var listener = CreateListener(counts);

        var cache = new MemoryDistributedCache(
            new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        await cache.GetOrSetJsonAsync(
            "telemetry:test",
            async _ =>
            {
                await Task.CompletedTask;
                return ("value", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                });
            },
            "test.category");

        await cache.GetOrSetJsonAsync(
            "telemetry:test",
            async _ =>
            {
                await Task.CompletedTask;
                return ("value", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                });
            },
            "test.category");

        Assert.True(counts.GetValueOrDefault("cache.misses") >= 1);
        Assert.True(counts.GetValueOrDefault("cache.sets") >= 1);
        Assert.True(counts.GetValueOrDefault("cache.hits") >= 1);
    }

    [Fact]
    public async Task RemoveJsonAsync_records_removes()
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        using var listener = CreateListener(counts);

        var cache = new MemoryDistributedCache(
            new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        await cache.SetJsonAsync(
            "telemetry:remove",
            "x",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) },
            "test.category");
        await cache.RemoveJsonAsync("telemetry:remove", "test.category");

        Assert.True(counts.GetValueOrDefault("cache.removes") >= 1);
    }

    private static MeterListener CreateListener(Dictionary<string, long> counts)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CacheTelemetry.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            counts[instrument.Name] = counts.GetValueOrDefault(instrument.Name) + measurement);

        listener.Start();
        _ = CacheTelemetry.Hits;
        return listener;
    }
}
