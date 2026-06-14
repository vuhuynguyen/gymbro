using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Shared.DomainPrimitives;
using Microsoft.EntityFrameworkCore;
using WebApi.Persistence;
using Xunit;

namespace Gymbro.Tests.Persistence;

/// <summary>
/// Guards the EF global-filter invariant. <c>AppDbContext.ApplyGlobalFilters</c>
/// only attaches a tenant / shared / soft-delete query filter to an entity that implements
/// <see cref="ITenantEntity"/>, <see cref="ISharedEntity"/>, or <see cref="ISoftDelete"/>; everything else
/// is persisted with NO filter. A tenant-owned entity that silently lacks a marker would be readable
/// across tenants. These tests fail the build if a <see cref="BaseEntity"/> mapped into AppDbContext is
/// neither filtered nor on the documented unfiltered allowlist — forcing a conscious decision (the same
/// "documented seam" pattern as <c>TenantAuthorizationExemptions</c>).
///
/// Pure model inspection — builds the model via the design-time factory, so it needs no database/Docker.
/// </summary>
public sealed class EntityFilterConventionTests
{
    [Fact]
    public void Every_mapped_entity_is_filtered_or_documented_as_intentionally_unfiltered()
    {
        using var context = new AppDbContextFactory().CreateDbContext(Array.Empty<string>());

        var unaccounted = MappedBaseEntityTypes(context)
            .Where(t => !typeof(ITenantEntity).IsAssignableFrom(t)
                && !typeof(ISharedEntity).IsAssignableFrom(t)
                && !typeof(ISoftDelete).IsAssignableFrom(t)
                && !UnfilteredEntityExemptions.IsExempt(t.Name))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unaccounted.Count == 0,
            "These AppDbContext entities receive NO global query filter and are not documented in "
            + "UnfilteredEntityExemptions. Implement ITenantEntity / ISharedEntity / ISoftDelete, or add a "
            + "reasoned exemption: " + string.Join(", ", unaccounted));
    }

    [Fact]
    public void Every_unfiltered_exemption_documents_a_reason()
    {
        var undocumented = UnfilteredEntityExemptions.All
            .Where(e => string.IsNullOrWhiteSpace(e.Value) || e.Value.Trim().Length < 20)
            .Select(e => e.Key)
            .ToList();

        Assert.True(
            undocumented.Count == 0,
            "Every UnfilteredEntityExemptions entry must explain why it is safe without a tenant filter: "
            + string.Join(", ", undocumented));
    }

    [Fact]
    public void Exemptions_do_not_list_an_entity_that_is_actually_filtered()
    {
        using var context = new AppDbContextFactory().CreateDbContext(Array.Empty<string>());
        var byName = MappedBaseEntityTypes(context).ToDictionary(t => t.Name, t => t, StringComparer.Ordinal);

        var stale = UnfilteredEntityExemptions.All.Keys
            .Where(name => byName.TryGetValue(name, out var t)
                && (typeof(ITenantEntity).IsAssignableFrom(t)
                    || typeof(ISharedEntity).IsAssignableFrom(t)
                    || typeof(ISoftDelete).IsAssignableFrom(t)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            stale.Count == 0,
            "These entities are filtered and must be removed from UnfilteredEntityExemptions: "
            + string.Join(", ", stale));
    }

    private static IEnumerable<Type> MappedBaseEntityTypes(DbContext context) =>
        context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t))
            .Distinct();
}

/// <summary>
/// Allowlist of <see cref="BaseEntity"/> types mapped by AppDbContext that intentionally receive no global
/// query filter, each with the reason it is safe. Mirrors the <c>TenantAuthorizationExemptions</c> seam:
/// a new unfiltered entity must be added here with a justification, or the convention test fails.
/// </summary>
internal static class UnfilteredEntityExemptions
{
    private static readonly IReadOnlyDictionary<string, string> Entries =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UserTenantRole"] =
                "Membership join row read by TenantResolutionMiddleware BEFORE any tenant is established, "
                + "so it cannot itself be tenant-filtered; every handler that reads it scopes by (userId, tenantId).",
            ["Invite"] =
                "Redeemed by code before the caller is a member of the target tenant (JoinTenantHandler), so a "
                + "tenant filter would break redemption; all owner-facing reads scope explicitly by tenant "
                + "(GetByCodeAndTenantAsync / GetByTenantAsync).",

            // Exercise is ISharedEntity (global catalog; the ISharedEntity shape also anticipates tenant-owned
            // exercises). Its child rows below are plain BaseEntity. This is safe ONLY while exercises are
            // global-only: CreateExerciseHandler uses Exercise.CreateGlobal and create/update/delete are
            // IPlatformAdminRequest. If tenant-owned exercises are ever introduced, these children MUST gain
            // their own tenant filter and be removed from this list — otherwise a direct child-table query
            // would leak a tenant-owned exercise's details across tenants.
            ["ExerciseInstruction"] = "Child row of the global Exercise catalog; safe only while exercises are global-only (see note above).",
            ["ExerciseMedia"] = "Child row of the global Exercise catalog; safe only while exercises are global-only (see note above).",
            ["ExerciseMuscle"] = "Child row of the global Exercise catalog; safe only while exercises are global-only (see note above).",
            ["ExerciseTag"] = "Child row of the global Exercise catalog; safe only while exercises are global-only (see note above).",
            ["ExerciseWarning"] = "Child row of the global Exercise catalog; safe only while exercises are global-only (see note above).",
        };

    public static bool IsExempt(string entityName) => Entries.ContainsKey(entityName);

    public static IReadOnlyDictionary<string, string> All => Entries;
}
