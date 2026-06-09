using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Shared.DomainPrimitives;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Entities;
using Modules.ExerciseModule.Infrastructure.Seeding;
using Modules.WorkoutPlanModule.Entities;
using Modules.WorkoutSessionModule.Entities;

namespace WebApi.Composition;

/// <summary>How the file-based exercise master data is applied to the global catalog.</summary>
public enum ExerciseSeedMode
{
    /// <summary>Non-destructive: insert exercises whose canonical name is not already present; never touch
    /// existing rows. Idempotent and safe in every environment. Runs at startup.</summary>
    InsertMissing,

    /// <summary>Destructive refresh: upsert every seed exercise by canonical name (update in place, preserving
    /// its Id so workout plans/logs stay valid; reactivate if previously soft-deleted) and <b>soft-delete</b>
    /// any global exercise not present in the seed set. Triggered explicitly via the <c>--reseed-exercises</c>
    /// CLI entrypoint. See <c>docs/master-data/EXERCISE_SEEDING.md</c>.</summary>
    Reseed
}

/// <summary>Counts from a seed run (for logging and the developer report).</summary>
public sealed record ExerciseSeedReport(
    int Inserted,
    int Updated,
    int Reactivated,
    int SkippedExisting,
    int SkippedInactive,
    int PrunedObsolete,
    int PrunedButReferenced)
{
    public override string ToString() =>
        $"inserted={Inserted}, updated={Updated}, reactivated={Reactivated}, skipped-existing={SkippedExisting}, " +
        $"skipped-inactive={SkippedInactive}, pruned-obsolete={PrunedObsolete}, pruned-but-referenced={PrunedButReferenced}";
}

/// <summary>
/// Seeds the global exercise catalog from the embedded master-data files (never from hardcoded C#).
/// Load → validate (fail fast, no partial import) → apply in a single atomic <c>SaveChanges</c> → invalidate cache.
///
/// <para><b>Data safety.</b> User workout logs and plans are never destroyed. The
/// <c>PerformedExercise.ExerciseId</c> and <c>PlanWorkoutExercise.ExerciseId</c> foreign keys use
/// <c>OnDelete(Restrict)</c>, so the database itself blocks physical deletion of a referenced exercise. This
/// seeder therefore never hard-deletes: <see cref="ExerciseSeedMode.Reseed"/> upserts seed rows in place
/// (preserving Ids) and <i>soft-deletes</i> obsolete ones — the same semantics as deleting an exercise through
/// the API. Logged history keeps its denormalized snapshot regardless.</para>
/// </summary>
public static class ExerciseMasterDataSeeder
{
    /// <summary>Resolves a scope and runs the seeder. Returns the counts.</summary>
    public static async Task<ExerciseSeedReport> RunAsync(
        IServiceProvider services,
        ExerciseSeedMode mode,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ExerciseMasterDataSeeder));
        var db = sp.GetRequiredService<AppDbContext>();
        var cache = sp.GetRequiredService<ExerciseCatalogCache>();
        return await SeedAsync(db, cache, logger, mode, cancellationToken);
    }

    public static async Task<ExerciseSeedReport> SeedAsync(
        AppDbContext db,
        ExerciseCatalogCache cache,
        ILogger logger,
        ExerciseSeedMode mode,
        CancellationToken cancellationToken = default)
    {
        // 1. Load + validate the files. Fail fast — nothing is written if the data is invalid.
        var data = new ExerciseSeedDataLoader().Load();
        var validation = new ExerciseSeedDataValidator().Validate(data);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                logger.LogError("Exercise seed validation error: {Error}", error);

            throw new InvalidOperationException(
                $"Exercise seed data failed validation with {validation.Errors.Count} error(s); no data was changed. " +
                "See the logged errors above.");
        }

        var activeSeed = data.Exercises.Where(e => e.IsActive).ToList();
        var skippedInactive = data.Exercises.Count - activeSeed.Count;
        var seedNames = activeSeed
            .Select(e => e.Name!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. Load existing GLOBAL exercises (TenantId == null), including soft-deleted, with children.
        //    Tenant-scoped (custom) exercises are never touched.
        var existing = await db.Exercises
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .Include(x => x.Muscles)
            .Include(x => x.Instructions)
            .Include(x => x.Tags)
            .Include(x => x.Warnings)
            .Include(x => x.Media)
            .ToListAsync(cancellationToken);

        var existingByName = new Dictionary<string, Exercise>(StringComparer.OrdinalIgnoreCase);
        var duplicateExisting = new List<Exercise>();
        foreach (var ex in existing)
        {
            if (!existingByName.TryAdd(ex.DefaultName, ex))
                duplicateExisting.Add(ex); // same name twice in the catalog — keep the first, prune the rest on reseed
        }

        // 3. Which exercises are referenced by user data (logs / plans)? Used to flag — never to delete.
        var referencedIds = new HashSet<Guid>();
        referencedIds.UnionWith(await db.PerformedExercises
            .IgnoreQueryFilters().Select(x => x.ExerciseId).Distinct().ToListAsync(cancellationToken));
        referencedIds.UnionWith(await db.Set<PlanWorkoutExercise>()
            .IgnoreQueryFilters().Select(x => x.ExerciseId).Distinct().ToListAsync(cancellationToken));

        int inserted = 0, updated = 0, reactivated = 0, skippedExisting = 0, pruned = 0, prunedReferenced = 0;
        var changedIds = new List<Guid>();
        var now = DateTimeOffset.UtcNow;

        // 4. Reseed prunes obsolete global exercises (soft-delete only — never a hard delete).
        if (mode == ExerciseSeedMode.Reseed)
        {
            var obsolete = existing
                .Where(x => !seedNames.Contains(x.DefaultName))
                .Concat(duplicateExisting)
                .Distinct();

            foreach (var ex in obsolete)
            {
                if (ex is ISoftDelete { IsDeleted: true })
                    continue; // already retired

                SoftDelete(ex, now);
                changedIds.Add(ex.Id);
                pruned++;
                if (referencedIds.Contains(ex.Id))
                {
                    prunedReferenced++;
                    logger.LogWarning(
                        "Reseed soft-deleted exercise '{Name}' ({Id}) which is still referenced by a workout " +
                        "plan or logged session. Its row is preserved (FK-safe) and logged history keeps its " +
                        "snapshot, but it will no longer appear in the catalog.",
                        ex.DefaultName, ex.Id);
                }
            }
        }

        // 5. Apply each active seed exercise.
        foreach (var dto in activeSeed)
        {
            var name = dto.Name!.Trim();
            if (existingByName.TryGetValue(name, out var match))
            {
                if (mode == ExerciseSeedMode.InsertMissing)
                {
                    skippedExisting++;
                    continue; // non-destructive: leave existing rows (incl. admin edits) untouched
                }

                // Reseed: refresh in place (Id preserved → references stay valid).
                if (match is ISoftDelete { IsDeleted: true })
                {
                    Reactivate(match);
                    reactivated++;
                }
                else
                {
                    updated++;
                }

                ExerciseSeedFactory.Apply(match, dto);
                changedIds.Add(match.Id);
            }
            else
            {
                var created = ExerciseSeedFactory.Create(dto);
                db.Exercises.Add(created);
                changedIds.Add(created.Id);
                inserted++;
            }
        }

        // 6. Persist atomically. EF wraps SaveChanges in a transaction, so a failure here rolls everything back
        //    (no partial import). The ISoftDelete soft-delete/audit interceptor runs as part of this commit.
        await db.SaveChangesAsync(cancellationToken);

        // 7. Invalidate the catalog cache (best-effort — a cache fault must not fail the committed seed).
        try
        {
            await cache.InvalidateSearchAsync(cancellationToken);
            foreach (var id in changedIds)
                await cache.InvalidateDetailAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Exercise catalog cache invalidation after seeding failed (non-fatal).");
        }

        var report = new ExerciseSeedReport(
            inserted, updated, reactivated, skippedExisting, skippedInactive, pruned, prunedReferenced);
        logger.LogInformation("Exercise master-data seed ({Mode}) complete: {Report}", mode, report);
        return report;
    }

    private static void SoftDelete(Exercise exercise, DateTimeOffset now)
    {
        var sd = (ISoftDelete)exercise;
        sd.IsDeleted = true;
        sd.DeletedOnUtc = now;
    }

    private static void Reactivate(Exercise exercise)
    {
        var sd = (ISoftDelete)exercise;
        sd.IsDeleted = false;
        sd.DeletedOnUtc = null;
    }
}
