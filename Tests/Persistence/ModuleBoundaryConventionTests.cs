using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Modules.ExerciseModule;
using Modules.FoodModule;
using Modules.NutritionModule;
using Modules.UserModule;
using Modules.WorkoutPlanModule;
using Modules.WorkoutSessionModule;
using Xunit;

namespace Gymbro.Tests.Persistence;

/// <summary>
/// Build-breaking guard for the module-boundary contract in docs/ARCHITECTURE.md. Modules may
/// cross boundaries ONLY via MediatR query/command/notification contracts that live in another module's
/// <c>*.Application</c> namespace — never its <c>*.Entities</c> (domain) namespace, and feature modules must
/// never reach into the Identity module at all.
///
/// <para>Cross-module project references are broader than the contracts they expose (e.g. WorkoutSession
/// references the whole WorkoutPlan project, not a thin contracts assembly), so nothing structural stops a
/// developer from importing a foreign entity. This convention test closes that gap: it reads each module
/// assembly's metadata <c>TypeReference</c> table — every external type the IL touches, including method
/// bodies — so a leak cannot hide inside a handler. Pure metadata inspection; no database/Docker needed.</para>
///
/// <para>If a genuinely shared contract type currently lives under <c>*.Entities</c>, the fix is to MOVE it to
/// the owning module's <c>*.Application</c> namespace (as <c>PlanVisibilityMode</c> already is) — not to relax
/// this test.</para>
///
/// <para><b>Scope (and a known exemption).</b> This guards feature-module ↔ feature-module boundaries. It does
/// NOT scan <c>BuildingBlocks.Infrastructure.Persistence</c>, which currently owns the EF model (entity
/// configurations, <c>DbSet</c>s and repositories) and therefore deliberately references every module's
/// <c>*.Entities</c> — i.e. the persistence kernel depends on the feature modules rather than the reverse. That
/// inversion is a recognised structural item: were each module to own its own persistence, this test should be
/// widened to scan the kernel too. The exemption is documented here so the green result is not mistaken for a
/// guarantee that <i>nothing</i> in the solution references module entities.</para>
/// </summary>
public sealed class ModuleBoundaryConventionTests
{
    private sealed record FeatureModule(string Name, Assembly Assembly, string EntitiesNamespace);

    private static readonly FeatureModule[] Modules =
    [
        new("Exercise", ExerciseModuleAssembly.Assembly, "Modules.ExerciseModule.Entities"),
        new("Food", FoodModuleAssembly.Assembly, "Modules.FoodModule.Entities"),
        new("Nutrition", NutritionModuleAssembly.Assembly, "Modules.NutritionModule.Entities"),
        new("User", UserModuleAssembly.Assembly, "Modules.UserModule.Entities"),
        new("WorkoutPlan", WorkoutPlanModuleAssembly.Assembly, "Modules.WorkoutPlanModule.Entities"),
        new("WorkoutSession", WorkoutSessionModuleAssembly.Assembly, "Modules.WorkoutSessionModule.Entities"),
    ];

    [Fact]
    public void No_module_references_another_modules_Entities_namespace()
    {
        var foreignEntityNamespaces = Modules
            .Select(m => m.EntitiesNamespace)
            .ToHashSet(StringComparer.Ordinal);

        var violations = new List<string>();

        foreach (var module in Modules)
        {
            // A module's OWN entities are TypeDefinitions (not TypeReferences), so anything from an
            // *.Entities namespace that shows up here is necessarily a FOREIGN module's domain type.
            foreach (var ns in ReferencedNamespaces(module.Assembly))
            {
                if (foreignEntityNamespaces.Contains(ns)
                    && !ns.Equals(module.EntitiesNamespace, StringComparison.Ordinal))
                    violations.Add($"{module.Name} -> {ns}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Module-boundary leak: a module references another module's *.Entities namespace. Cross-module "
            + "communication must go through MediatR contracts in the owning module's *.Application namespace "
            + "(docs/ARCHITECTURE.md). Offending references: " + string.Join(", ", violations.Distinct()));
    }

    [Fact]
    public void No_feature_module_references_the_Identity_module()
    {
        var violations = new List<string>();

        foreach (var module in Modules)
        {
            foreach (var ns in ReferencedNamespaces(module.Assembly))
            {
                if (ns.StartsWith("Modules.IdentityModule", StringComparison.Ordinal))
                    violations.Add($"{module.Name} -> {ns}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Feature modules must not reference the Identity module (including IdentityDbContext). Identity "
            + "integrates only by publishing the UserRegistered/UserDeleted MediatR notifications, which the "
            + "User module handles. Offending references: " + string.Join(", ", violations.Distinct()));
    }

    /// <summary>
    /// Every distinct namespace referenced by <paramref name="assembly"/> via its metadata TypeRef table.
    /// TypeReferences are types defined elsewhere that this assembly uses anywhere in its IL, so this catches
    /// boundary leaks in signatures AND method bodies — stricter than scanning <c>using</c> directives.
    /// </summary>
    private static IReadOnlyCollection<string> ReferencedNamespaces(Assembly assembly)
    {
        var path = assembly.Location;
        Assert.False(
            string.IsNullOrEmpty(path),
            $"Cannot inspect {assembly.GetName().Name}: assembly has no on-disk location (single-file publish?).");

        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in reader.TypeReferences)
        {
            var typeRef = reader.GetTypeReference(handle);
            var ns = reader.GetString(typeRef.Namespace);
            if (!string.IsNullOrEmpty(ns))
                namespaces.Add(ns);
        }

        return namespaces;
    }
}
