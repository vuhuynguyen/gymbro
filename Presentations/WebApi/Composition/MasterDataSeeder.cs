using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Shared.DomainPrimitives;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Composition;

/// <summary>How file-based master data is applied to a global catalog (exercise or food).</summary>
public enum MasterDataSeedMode
{
    /// <summary>Non-destructive: insert rows whose canonical name is not already present; never touch existing
    /// rows. Idempotent and safe in every environment. Runs at startup.</summary>
    InsertMissing,

    /// <summary>Destructive refresh: upsert every seed row by canonical name (update in place, preserving its Id
    /// so user references stay valid; reactivate if previously soft-deleted) and <b>soft-delete</b> any global
    /// row not present in the seed set. Triggered explicitly via a CLI <c>--reseed-*</c> entrypoint.</summary>
    Reseed
}

/// <summary>Counts from a seed run (for logging and the developer report).</summary>
public sealed record MasterDataSeedReport(
    int Inserted, int Updated, int Reactivated, int SkippedExisting,
    int SkippedInactive, int PrunedObsolete, int PrunedButReferenced)
{
    public override string ToString() =>
        $"inserted={Inserted}, updated={Updated}, reactivated={Reactivated}, skipped-existing={SkippedExisting}, " +
        $"skipped-inactive={SkippedInactive}, pruned-obsolete={PrunedObsolete}, pruned-but-referenced={PrunedButReferenced}";
}

/// <summary>
/// The per-catalog parts of a seed run — everything that differs between the exercise and food catalogs. The
/// orchestration around these (load → prune-obsolete → upsert-by-name → atomic save → cache-invalidate) is shared
/// in <see cref="MasterDataSeeder.SeedAsync{TEntity,TDto}"/>.
/// </summary>
public sealed record MasterDataSeedSpec<TEntity, TDto>(
    string Label,
    Func<ILogger, (IReadOnlyList<TDto> ActiveSeed, int SkippedInactive, HashSet<string> SeedNames)> LoadAndValidate,
    Func<AppDbContext, CancellationToken, Task<List<TEntity>>> LoadExistingGlobals,
    Func<TEntity, string> EntityName,
    Func<TDto, string> DtoName,
    Func<AppDbContext, CancellationToken, Task<HashSet<Guid>>> LoadReferencedIds,
    Func<TDto, TEntity> Create,
    Action<TEntity, TDto> Apply,
    Func<TEntity, Guid> EntityId,
    Action<AppDbContext, TEntity> Add,
    Func<CancellationToken, Task> InvalidateSearch,
    Func<Guid, CancellationToken, Task> InvalidateDetail)
    where TEntity : class, ISoftDelete;

/// <summary>
/// Seeds a global master-data catalog from embedded files (never from hardcoded C#): load → validate (fail fast,
/// no partial import) → apply in a single atomic <c>SaveChanges</c> → invalidate cache. Shared by the exercise and
/// food catalogs via <see cref="MasterDataSeedSpec{TEntity,TDto}"/>.
///
/// <para><b>Data safety.</b> User data is never destroyed. The FKs into a catalog use <c>OnDelete(Restrict)</c>,
/// so the database itself blocks physical deletion of a referenced row. This seeder therefore never hard-deletes:
/// <see cref="MasterDataSeedMode.Reseed"/> upserts seed rows in place (preserving Ids) and <i>soft-deletes</i>
/// obsolete ones — the same semantics as deleting through the API; logged history keeps its denormalized snapshot.</para>
/// </summary>
public static class MasterDataSeeder
{
    public static async Task<MasterDataSeedReport> SeedAsync<TEntity, TDto>(
        AppDbContext db,
        ILogger logger,
        MasterDataSeedMode mode,
        MasterDataSeedSpec<TEntity, TDto> spec,
        CancellationToken cancellationToken = default)
        where TEntity : class, ISoftDelete
    {
        // 1. Load + validate the files. Fail fast — nothing is written if the data is invalid.
        var (activeSeed, skippedInactive, seedNames) = spec.LoadAndValidate(logger);

        // 2. Load existing GLOBAL rows (TenantId == null), including soft-deleted. Tenant-custom rows are untouched.
        var existing = await spec.LoadExistingGlobals(db, cancellationToken);

        var existingByName = new Dictionary<string, TEntity>(StringComparer.OrdinalIgnoreCase);
        var duplicateExisting = new List<TEntity>();
        foreach (var entity in existing)
            if (!existingByName.TryAdd(spec.EntityName(entity), entity))
                duplicateExisting.Add(entity); // same name twice in the catalog — keep the first, prune the rest on reseed

        // 3. Which rows are referenced by user data? Used to flag — never to delete.
        var referencedIds = await spec.LoadReferencedIds(db, cancellationToken);

        int inserted = 0, updated = 0, reactivated = 0, skippedExisting = 0, pruned = 0, prunedReferenced = 0;
        var changedIds = new List<Guid>();
        var now = DateTimeOffset.UtcNow;

        // 4. Reseed prunes obsolete global rows (soft-delete only — never a hard delete).
        if (mode == MasterDataSeedMode.Reseed)
        {
            var obsolete = existing
                .Where(x => !seedNames.Contains(spec.EntityName(x)))
                .Concat(duplicateExisting)
                .Distinct();

            foreach (var entity in obsolete)
            {
                if (entity.IsDeleted) continue; // already retired

                entity.IsDeleted = true;
                entity.DeletedOnUtc = now;
                changedIds.Add(spec.EntityId(entity));
                pruned++;
                if (referencedIds.Contains(spec.EntityId(entity)))
                {
                    prunedReferenced++;
                    logger.LogWarning(
                        "Reseed soft-deleted {Label} '{Name}' ({Id}) which is still referenced by user data. Its row "
                        + "is preserved (FK-safe) and logged history keeps its snapshot, but it leaves the catalog.",
                        spec.Label, spec.EntityName(entity), spec.EntityId(entity));
                }
            }
        }

        // 5. Apply each active seed row.
        foreach (var dto in activeSeed)
        {
            var name = spec.DtoName(dto);
            if (existingByName.TryGetValue(name, out var match))
            {
                if (mode == MasterDataSeedMode.InsertMissing)
                {
                    skippedExisting++;
                    continue; // non-destructive: leave existing rows (incl. admin edits) untouched
                }

                // Reseed: refresh in place (Id preserved → references stay valid).
                if (match.IsDeleted)
                {
                    match.IsDeleted = false;
                    match.DeletedOnUtc = null;
                    reactivated++;
                }
                else
                {
                    updated++;
                }

                spec.Apply(match, dto);
                changedIds.Add(spec.EntityId(match));
            }
            else
            {
                var created = spec.Create(dto);
                spec.Add(db, created);
                changedIds.Add(spec.EntityId(created));
                inserted++;
            }
        }

        // 6. Persist atomically (EF wraps SaveChanges in a transaction — no partial import).
        await db.SaveChangesAsync(cancellationToken);

        // 7. Invalidate the catalog cache (best-effort — a cache fault must not fail the committed seed).
        try
        {
            await spec.InvalidateSearch(cancellationToken);
            foreach (var id in changedIds)
                await spec.InvalidateDetail(id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Label} catalog cache invalidation after seeding failed (non-fatal).", spec.Label);
        }

        var report = new MasterDataSeedReport(
            inserted, updated, reactivated, skippedExisting, skippedInactive, pruned, prunedReferenced);
        logger.LogInformation("{Label} master-data seed ({Mode}) complete: {Report}", spec.Label, mode, report);
        return report;
    }
}
