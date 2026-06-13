using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Caching;
using Modules.FoodModule.Entities;
using Modules.FoodModule.Infrastructure.Seeding;
using Modules.NutritionModule.Entities;

namespace WebApi.Composition;

/// <summary>
/// Seeds the global food/supplement catalog from the embedded master-data file. The load → prune → upsert → save →
/// invalidate orchestration lives in <see cref="MasterDataSeeder"/>; this supplies the food-specific spec.
/// User plans/logs are never destroyed — <c>PlanMealItem</c>/<c>LoggedItem</c> FKs are <c>Restrict</c>, so a
/// reseed soft-deletes obsolete rows rather than dropping referenced ones.
/// </summary>
public static class FoodMasterDataSeeder
{
    public static async Task<MasterDataSeedReport> RunAsync(
        IServiceProvider services, MasterDataSeedMode mode, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FoodMasterDataSeeder));
        var db = sp.GetRequiredService<AppDbContext>();
        var cache = sp.GetRequiredService<FoodCatalogCache>();
        return await SeedAsync(db, cache, logger, mode, cancellationToken);
    }

    public static Task<MasterDataSeedReport> SeedAsync(
        AppDbContext db, FoodCatalogCache cache, ILogger logger, MasterDataSeedMode mode,
        CancellationToken cancellationToken = default) =>
        MasterDataSeeder.SeedAsync(db, logger, mode, Spec(cache), cancellationToken);

    private static MasterDataSeedSpec<Food, FoodSeedDto> Spec(FoodCatalogCache cache) => new(
        Label: "Food",
        LoadAndValidate: logger =>
        {
            var data = new FoodSeedDataLoader().Load();
            var validation = new FoodSeedDataValidator().Validate(data);
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                    logger.LogError("Food seed validation error: {Error}", error);
                throw new InvalidOperationException(
                    $"Food seed data failed validation with {validation.Errors.Count} error(s); no data was changed.");
            }

            var active = data.Foods.Where(f => f.IsActive).ToList();
            var names = active.Select(f => f.Name!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return (active, data.Foods.Count - active.Count, names);
        },
        LoadExistingGlobals: (db, ct) => db.Foods
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .ToListAsync(ct),
        EntityName: f => f.Name,
        DtoName: dto => dto.Name!.Trim(),
        LoadReferencedIds: async (db, ct) =>
        {
            var ids = new HashSet<Guid>();
            ids.UnionWith(await db.Set<PlanMealItem>()
                .IgnoreQueryFilters().Select(x => x.FoodId).Distinct().ToListAsync(ct));
            ids.UnionWith(await db.Set<LoggedItem>()
                .IgnoreQueryFilters().Where(x => x.FoodId != null).Select(x => x.FoodId!.Value).Distinct().ToListAsync(ct));
            return ids;
        },
        Create: FoodSeedFactory.Create,
        Apply: FoodSeedFactory.Apply,
        EntityId: f => f.Id,
        Add: (db, f) => db.Foods.Add(f),
        InvalidateSearch: cache.InvalidateSearchAsync,
        InvalidateDetail: cache.InvalidateDetailAsync);
}
