using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Composition;
using Xunit;

namespace Gymbro.Tests.Caching;

/// <summary>
/// Regression guard for the memory-provider DI wiring. <c>DistributedCacheGenerationCounter</c> depends on
/// <c>IConnectionMultiplexer</c>, which is only registered in the Redis branch. Before the fix the counter
/// was registered type-based, so resolving <see cref="ICacheGenerationCounter"/> in memory mode threw
/// "Unable to resolve service for type 'IConnectionMultiplexer'" — crashing every exercise catalog read on
/// any <c>Cache:Provider=Memory</c> deployment. This test needs no Docker, so unlike the integration cache
/// tests it actually runs in CI.
/// </summary>
public sealed class GenerationCounterMemoryProviderTests
{
    [Fact]
    public async Task Memory_provider_resolves_and_increments_the_generation_counter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // The documented Redis-less fallback. Use a non-Test environment to prove it works on a
                // real single-node deployment, not just because the Test environment forces memory.
                ["Cache:Provider"] = "Memory"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGymBroDistributedInfrastructure(configuration, "Production");

        await using var provider = services.BuildServiceProvider();

        var counter = provider.GetRequiredService<ICacheGenerationCounter>();

        Assert.Equal(0, await counter.GetAsync("test:gen"));
        Assert.Equal(1, await counter.IncrementAsync("test:gen"));
        Assert.Equal(1, await counter.GetAsync("test:gen"));
    }
}
