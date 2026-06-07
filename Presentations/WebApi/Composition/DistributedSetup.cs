using System.Threading.RateLimiting;
using BuildingBlocks.Application.Caching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using RedisRateLimiting;
using StackExchange.Redis;

namespace WebApi.Composition;

/// <summary>
/// Generation counters for cache-bust invalidation. Uses Redis INCR when Redis is configured;
/// otherwise read-modify-write via <see cref="IDistributedCache"/> when memory is explicitly selected.
/// </summary>
internal sealed class DistributedCacheGenerationCounter(
    IDistributedCache cache,
    ICacheKeyNamespace keyNamespace,
    IConnectionMultiplexer? redis) : ICacheGenerationCounter
{
    // A cache fault degrades to generation 0: the read then falls through to the DB (the GetOrSet factory),
    // so a Redis outage slows catalog reads but never fails them.
    public async Task<long> GetAsync(string logicalKey, CancellationToken cancellationToken = default)
    {
        var key = keyNamespace.Qualify(logicalKey);
        try
        {
            if (redis is not null)
            {
                var value = await redis.GetDatabase().StringGetAsync(key);
                return value.HasValue ? (long)value : 0;
            }

            var text = await cache.GetStringAsync(key, cancellationToken);
            return text is null ? 0 : long.Parse(text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CacheTelemetry.RecordError("generation");
            return 0;
        }
    }

    // Best-effort: invalidation must not fail a write that already committed. If the bump is lost (Redis
    // down) the old entries still expire on their own TTL.
    public async Task<long> IncrementAsync(string logicalKey, CancellationToken cancellationToken = default)
    {
        var key = keyNamespace.Qualify(logicalKey);
        try
        {
            if (redis is not null)
                return await redis.GetDatabase().StringIncrementAsync(key);

            var next = await GetAsync(logicalKey, cancellationToken) + 1;
            await cache.SetStringAsync(
                key,
                next.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CachePolicies.GenerationCounter
                },
                cancellationToken);
            return next;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CacheTelemetry.RecordError("generation");
            return 0;
        }
    }
}

public static class DistributedSetup
{
    /// <summary>
    /// Registers <see cref="IDistributedCache"/>, generation counters, and auth rate limits.
    /// Requires <c>ConnectionStrings:Redis</c>, or in-memory when <c>Cache:Provider=Memory</c> or
    /// <paramref name="environmentName"/> is <c>Test</c> (integration tests).
    /// </summary>
    public static IServiceCollection AddGymBroDistributedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var cacheEnvironment = configuration["Cache:Environment"] ?? environmentName;
        services.AddSingleton<ICacheKeyNamespace>(
            _ => CacheKeyNamespace.FromEnvironment(cacheEnvironment));

        var redisConnection = configuration.GetConnectionString("Redis")
                              ?? configuration["Redis:ConnectionString"];
        var useRedis = !string.IsNullOrWhiteSpace(redisConnection);
        var useMemory = string.Equals(
                            configuration["Cache:Provider"],
                            "Memory",
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase);

        if (useRedis)
        {
            var multiplexer = ConnectionMultiplexer.Connect(redisConnection!);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () =>
                    Task.FromResult<IConnectionMultiplexer>(multiplexer);
            });
        }
        else if (useMemory)
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Redis is required. " +
                "To use in-memory cache instead, set Cache:Provider=Memory explicitly.");
        }

        // Factory registration (not type-based): IConnectionMultiplexer is only registered in the Redis
        // branch above, so in memory mode it must be resolved *optionally* via GetService (-> null).
        // Type-based AddSingleton would force constructor injection and throw "Unable to resolve
        // IConnectionMultiplexer" whenever Cache:Provider=Memory (and in the Test environment).
        services.AddSingleton<ICacheGenerationCounter>(sp =>
            new DistributedCacheGenerationCounter(
                sp.GetRequiredService<IDistributedCache>(),
                sp.GetRequiredService<ICacheKeyNamespace>(),
                sp.GetService<IConnectionMultiplexer>()));
        AddRateLimiting(services, useRedis);

        return services;
    }

    private static void AddRateLimiting(IServiceCollection services, bool useRedis)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsync(
                    "Too many requests. Please try again later.",
                    cancellationToken);
            };

            options.AddPolicy("auth", ctx => AuthPartition(ctx, useRedis, "auth", 10));
            options.AddPolicy("auth-refresh", ctx => AuthPartition(ctx, useRedis, "auth-refresh", 30));
            // Authenticated invite redemption. Partitioned per caller (see JoinPartition) so brute-forcing
            // invite codes is throttled per account (defense in depth; the 40-bit
            // CSPRNG codes already make online guessing infeasible).
            options.AddPolicy("tenant-join", ctx => JoinPartition(ctx, useRedis));
        });
    }

    private static RateLimitPartition<string> AuthPartition(
        HttpContext httpContext,
        bool useRedis,
        string policyName,
        int permitLimit)
    {
        var keyNamespace = httpContext.RequestServices.GetRequiredService<ICacheKeyNamespace>();
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = keyNamespace.Qualify(CacheKeys.AuthRateLimit(policyName, clientIp));
        return FixedWindow(httpContext, useRedis, partitionKey, permitLimit);
    }

    // Invite redemption is authenticated, so partition per caller (domainUserId) rather than per IP: one
    // account's code-guessing is throttled regardless of source IP, and one abusive account can't
    // rate-limit every legitimate user behind a shared NAT.
    private static RateLimitPartition<string> JoinPartition(HttpContext httpContext, bool useRedis)
    {
        var keyNamespace = httpContext.RequestServices.GetRequiredService<ICacheKeyNamespace>();
        var caller = httpContext.User.FindFirst("domainUserId")?.Value
                     ?? httpContext.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        var partitionKey = keyNamespace.Qualify(CacheKeys.AuthRateLimit("tenant-join", caller));
        return FixedWindow(httpContext, useRedis, partitionKey, permitLimit: 10);
    }

    private static RateLimitPartition<string> FixedWindow(
        HttpContext httpContext,
        bool useRedis,
        string partitionKey,
        int permitLimit)
    {
        var window = TimeSpan.FromMinutes(1);

        if (useRedis)
        {
            var muxer = httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            return RedisRateLimitPartition.GetFixedWindowRateLimiter(
                partitionKey,
                _ => new RedisFixedWindowRateLimiterOptions
                {
                    ConnectionMultiplexerFactory = () => muxer,
                    PermitLimit = permitLimit,
                    Window = window
                });
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0
            });
    }
}
