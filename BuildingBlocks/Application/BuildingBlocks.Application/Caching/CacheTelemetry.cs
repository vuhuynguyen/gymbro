using System.Diagnostics.Metrics;

namespace BuildingBlocks.Application.Caching;

/// <summary>OpenTelemetry-compatible counters for distributed cache operations.</summary>
public static class CacheTelemetry
{
    public const string MeterName = "GymBro.Cache";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> Hits =
        Meter.CreateCounter<long>("cache.hits", description: "Distributed cache hits");

    public static readonly Counter<long> Misses =
        Meter.CreateCounter<long>("cache.misses", description: "Distributed cache misses");

    public static readonly Counter<long> Sets =
        Meter.CreateCounter<long>("cache.sets", description: "Distributed cache writes");

    public static readonly Counter<long> Removes =
        Meter.CreateCounter<long>("cache.removes", description: "Distributed cache key removals");

    public static readonly Counter<long> Errors =
        Meter.CreateCounter<long>("cache.errors", description: "Distributed cache faults (degraded to source)");

    internal static void RecordHit(string category) =>
        Hits.Add(1, new KeyValuePair<string, object?>("category", category));

    internal static void RecordMiss(string category) =>
        Misses.Add(1, new KeyValuePair<string, object?>("category", category));

    internal static void RecordSet(string category) =>
        Sets.Add(1, new KeyValuePair<string, object?>("category", category));

    internal static void RecordRemove(string category) =>
        Removes.Add(1, new KeyValuePair<string, object?>("category", category));

    /// <summary>Public so cross-assembly cache adapters (e.g. the generation counter) can report faults.</summary>
    public static void RecordError(string category) =>
        Errors.Add(1, new KeyValuePair<string, object?>("category", category));
}
