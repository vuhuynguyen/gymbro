namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Qualifies module-scoped logical cache keys with environment and service segments so every
/// Redis key — whether accessed via <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// or raw <c>IConnectionMultiplexer</c> — shares one namespace.
/// </summary>
public interface ICacheKeyNamespace
{
    string Qualify(string logicalKey);
}

public sealed class CacheKeyNamespace : ICacheKeyNamespace
{
    private readonly string _prefix;

    public CacheKeyNamespace(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (!prefix.EndsWith(':'))
            prefix += ':';
        _prefix = prefix;
    }

    public static CacheKeyNamespace FromEnvironment(string environmentName, string serviceName = "gymbro") =>
        new($"{environmentName.ToLowerInvariant()}:{serviceName}");

    public string Qualify(string logicalKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);
        return $"{_prefix}{logicalKey}";
    }
}
