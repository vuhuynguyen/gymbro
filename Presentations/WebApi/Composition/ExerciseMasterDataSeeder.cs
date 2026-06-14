using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Entities;
using Modules.ExerciseModule.Infrastructure.Seeding;
using Modules.WorkoutPlanModule.Entities;
using Modules.WorkoutSessionModule.Entities;

namespace WebApi.Composition;

/// <summary>
/// Seeds the global exercise catalog from the embedded master-data files. The load → prune → upsert → save →
/// invalidate orchestration lives in <see cref="MasterDataSeeder"/>; this supplies the exercise-specific spec.
/// User workout logs/plans are never destroyed — <c>PerformedExercise</c>/<c>PlanWorkoutExercise</c> FKs are
/// <c>Restrict</c>, so a reseed soft-deletes obsolete rows rather than dropping referenced ones.
/// </summary>
public static class ExerciseMasterDataSeeder
{
    public static async Task<MasterDataSeedReport> RunAsync(
        IServiceProvider services, MasterDataSeedMode mode, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ExerciseMasterDataSeeder));
        var db = sp.GetRequiredService<AppDbContext>();
        var cache = sp.GetRequiredService<ExerciseCatalogCache>();
        return await SeedAsync(db, cache, logger, mode, cancellationToken);
    }

    public static Task<MasterDataSeedReport> SeedAsync(
        AppDbContext db, ExerciseCatalogCache cache, ILogger logger, MasterDataSeedMode mode,
        CancellationToken cancellationToken = default) =>
        MasterDataSeeder.SeedAsync(db, logger, mode, Spec(cache), cancellationToken);

    private static MasterDataSeedSpec<Exercise, ExerciseSeedDto> Spec(ExerciseCatalogCache cache) => new(
        Label: "Exercise",
        LoadAndValidate: logger =>
        {
            var data = new ExerciseSeedDataLoader().Load();
            var validation = new ExerciseSeedDataValidator().Validate(data);
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                    logger.LogError("Exercise seed validation error: {Error}", error);
                throw new InvalidOperationException(
                    $"Exercise seed data failed validation with {validation.Errors.Count} error(s); no data was changed.");
            }

            var active = data.Exercises.Where(e => e.IsActive).ToList();
            var names = active.Select(e => e.Name!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return (active, data.Exercises.Count - active.Count, names);
        },
        LoadExistingGlobals: (db, ct) => db.Set<Exercise>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .Include(x => x.Muscles)
            .Include(x => x.Instructions)
            .Include(x => x.Tags)
            .Include(x => x.Warnings)
            .Include(x => x.Media)
            .ToListAsync(ct),
        EntityName: e => e.DefaultName,
        DtoName: dto => dto.Name!.Trim(),
        LoadReferencedIds: async (db, ct) =>
        {
            var ids = new HashSet<Guid>();
            ids.UnionWith(await db.Set<PerformedExercise>()
                .IgnoreQueryFilters().Select(x => x.ExerciseId).Distinct().ToListAsync(ct));
            ids.UnionWith(await db.Set<PlanWorkoutExercise>()
                .IgnoreQueryFilters().Select(x => x.ExerciseId).Distinct().ToListAsync(ct));
            return ids;
        },
        Create: ExerciseSeedFactory.Create,
        Apply: ExerciseSeedFactory.Apply,
        EntityId: e => e.Id,
        Add: (db, e) => db.Set<Exercise>().Add(e),
        InvalidateSearch: cache.InvalidateSearchAsync,
        InvalidateDetail: cache.InvalidateDetailAsync);
}
