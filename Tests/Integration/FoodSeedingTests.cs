using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.FoodModule.Application.Caching;
using Modules.FoodModule.Entities;
using WebApi.Composition;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Verifies the food master-data seeder against a real database: it inserts the embedded global catalog and is
/// idempotent (a second InsertMissing run adds no duplicates). Skips when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FoodSeedingTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task Seeder_inserts_global_catalog_and_is_idempotent()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // First run inserts the embedded catalog into the global (TenantId == null) catalog.
        await fixture.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var cache = sp.GetRequiredService<FoodCatalogCache>();
            await FoodMasterDataSeeder.SeedAsync(db, cache, NullLogger.Instance, FoodSeedMode.InsertMissing);
        });

        var afterFirst = await CountGlobal("Chicken Breast, Cooked");
        Assert.Equal(1, afterFirst);

        // Second InsertMissing run is non-destructive: no duplicate is created.
        await fixture.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var cache = sp.GetRequiredService<FoodCatalogCache>();
            await FoodMasterDataSeeder.SeedAsync(db, cache, NullLogger.Instance, FoodSeedMode.InsertMissing);
        });

        Assert.Equal(1, await CountGlobal("Chicken Breast, Cooked"));
        // A supplement seeded too (proves FoodKind round-trips).
        Assert.Equal(1, await CountGlobal("Creatine Monohydrate"));
    }

    private Task<int> CountGlobal(string name) => fixture.InScopeAsync(sp =>
    {
        var db = sp.GetRequiredService<AppDbContext>();
        return db.Foods
            .IgnoreQueryFilters()
            .CountAsync(f => f.TenantId == null && !f.IsDeleted && f.Name == name);
    });
}
