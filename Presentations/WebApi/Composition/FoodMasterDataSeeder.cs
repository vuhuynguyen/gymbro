using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Shared.DomainPrimitives;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Entities;
using Modules.FoodModule.Infrastructure.Seeding;
using Modules.NutritionModule.Entities;

namespace WebApi.Composition;

/// <summary>How the file-based food master data is applied to the global catalog.</summary>
public enum FoodSeedMode
{
    /// <summary>Non-destructive: insert foods whose canonical name is not already present; never touch existing
    /// rows. Idempotent and safe in every environment. Runs at startup.</summary>
    InsertMissing,

    /// <summary>Destructive refresh: upsert every seed food by canonical name (update in place, preserving its
    /// Id so plan items / logged items stay valid; reactivate if previously soft-deleted) and <b>soft-delete</b>
    /// any global food not present in the seed set. Triggered via the <c>--reseed-foods</c> CLI entrypoint.</summary>
    Reseed
}

/// <summary>Counts from a seed run (for logging and the developer report).</summary>
public sealed record FoodSeedReport(
    int Inserted, int Updated, int Reactivated, int SkippedExisting,
    int SkippedInactive, int PrunedObsolete, int PrunedButReferenced)
{
    public override string ToString() =>
        $"inserted={Inserted}, updated={Updated}, reactivated={Reactivated}, skipped-existing={SkippedExisting}, " +
        $"skipped-inactive={SkippedInactive}, pruned-obsolete={PrunedObsolete}, pruned-but-referenced={PrunedButReferenced}";
}

/// <summary>
/// Seeds the global food/supplement catalog from the embedded master-data file (never from hardcoded C#).
/// Load → validate (fail fast, no partial import) → apply in a single atomic <c>SaveChanges</c>. Mirrors
/// <c>ExerciseMasterDataSeeder</c>.
///
/// <para><b>Data safety.</b> User plans/logs are never destroyed. <c>PlanMealItem.FoodId</c> and
/// <c>LoggedItem.FoodId</c> use <c>OnDelete(Restrict)</c>, so the database itself blocks physical deletion of a
/// referenced food. This seeder therefore never hard-deletes: <see cref="FoodSeedMode.Reseed"/> upserts seed
/// rows in place (preserving Ids) and <i>soft-deletes</i> obsolete ones — and logged items keep their
/// denormalized food snapshot regardless.</para>
/// </summary>
public static class FoodMasterDataSeeder
{
    public static async Task<FoodSeedReport> RunAsync(
        IServiceProvider services, FoodSeedMode mode, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FoodMasterDataSeeder));
        var db = sp.GetRequiredService<AppDbContext>();
        return await SeedAsync(db, logger, mode, cancellationToken);
    }

    public static async Task<FoodSeedReport> SeedAsync(
        AppDbContext db, ILogger logger, FoodSeedMode mode, CancellationToken cancellationToken = default)
    {
        // 1. Load + validate. Fail fast — nothing is written if the data is invalid.
        var data = new FoodSeedDataLoader().Load();
        var validation = new FoodSeedDataValidator().Validate(data);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                logger.LogError("Food seed validation error: {Error}", error);
            throw new InvalidOperationException(
                $"Food seed data failed validation with {validation.Errors.Count} error(s); no data was changed.");
        }

        var activeSeed = data.Foods.Where(f => f.IsActive).ToList();
        var skippedInactive = data.Foods.Count - activeSeed.Count;
        var seedNames = activeSeed.Select(f => f.Name!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. Load existing GLOBAL foods (TenantId == null), including soft-deleted. Tenant-custom foods untouched.
        var existing = await db.Foods
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);

        var existingByName = new Dictionary<string, Food>(StringComparer.OrdinalIgnoreCase);
        var duplicateExisting = new List<Food>();
        foreach (var food in existing)
            if (!existingByName.TryAdd(food.Name, food))
                duplicateExisting.Add(food);

        // 3. Which foods are referenced by user data (plan items / logged items)? Flag — never delete.
        var referencedIds = new HashSet<Guid>();
        referencedIds.UnionWith(await db.Set<PlanMealItem>()
            .IgnoreQueryFilters().Select(x => x.FoodId).Distinct().ToListAsync(cancellationToken));
        referencedIds.UnionWith(await db.Set<LoggedItem>()
            .IgnoreQueryFilters().Where(x => x.FoodId != null)
            .Select(x => x.FoodId!.Value).Distinct().ToListAsync(cancellationToken));

        int inserted = 0, updated = 0, reactivated = 0, skippedExisting = 0, pruned = 0, prunedReferenced = 0;
        var now = DateTimeOffset.UtcNow;

        // 4. Reseed prunes obsolete global foods (soft-delete only).
        if (mode == FoodSeedMode.Reseed)
        {
            var obsolete = existing
                .Where(x => !seedNames.Contains(x.Name))
                .Concat(duplicateExisting)
                .Distinct();

            foreach (var food in obsolete)
            {
                if (food is ISoftDelete { IsDeleted: true }) continue;
                SoftDelete(food, now);
                pruned++;
                if (referencedIds.Contains(food.Id))
                {
                    prunedReferenced++;
                    logger.LogWarning(
                        "Reseed soft-deleted food '{Name}' ({Id}) which is still referenced by a nutrition plan " +
                        "or logged item. Its row is preserved (FK-safe) and logged history keeps its snapshot.",
                        food.Name, food.Id);
                }
            }
        }

        // 5. Apply each active seed food.
        foreach (var dto in activeSeed)
        {
            var name = dto.Name!.Trim();
            if (existingByName.TryGetValue(name, out var match))
            {
                if (mode == FoodSeedMode.InsertMissing) { skippedExisting++; continue; }

                if (match is ISoftDelete { IsDeleted: true }) { Reactivate(match); reactivated++; }
                else updated++;
                FoodSeedFactory.Apply(match, dto);
            }
            else
            {
                db.Foods.Add(FoodSeedFactory.Create(dto));
                inserted++;
            }
        }

        // 6. Persist atomically (EF wraps SaveChanges in a transaction — no partial import).
        await db.SaveChangesAsync(cancellationToken);

        var report = new FoodSeedReport(
            inserted, updated, reactivated, skippedExisting, skippedInactive, pruned, prunedReferenced);
        logger.LogInformation("Food master-data seed ({Mode}) complete: {Report}", mode, report);
        return report;
    }

    private static void SoftDelete(Food food, DateTimeOffset now)
    {
        var sd = (ISoftDelete)food;
        sd.IsDeleted = true;
        sd.DeletedOnUtc = now;
    }

    private static void Reactivate(Food food)
    {
        var sd = (ISoftDelete)food;
        sd.IsDeleted = false;
        sd.DeletedOnUtc = null;
    }
}
