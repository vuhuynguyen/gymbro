namespace BuildingBlocks.Application.Caching;

/// <summary>Shared logical cache key segments (without environment/service prefix).</summary>
public static class CacheKeys
{
    /// <summary>
    /// Bump when key shape changes; old entries expire via their TTL. No manual Redis purge required.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>The schema-version key segment (e.g. <c>v1</c>). Every logical key starts with this.</summary>
    public static string Version => $"v{SchemaVersion}";

    /// <summary>
    /// Prefixes a module logical key with the schema version: <c>v{N}:{logicalKey}</c>. The single place
    /// the version segment is built — modules compose their own key bodies but never re-derive the prefix.
    /// </summary>
    public static string WithVersion(string logicalKey) => $"{Version}:{logicalKey}";

    /// <summary>Appends a generation suffix for cache-bust invalidation: <c>{logicalKey}:g{generation}</c>.</summary>
    public static string Versioned(string logicalKey, long generation) => $"{logicalKey}:g{generation}";

    public static string AuthRateLimit(string policy, string partitionKey) =>
        $"{Version}:ratelimit:{policy}:{partitionKey}";
}
