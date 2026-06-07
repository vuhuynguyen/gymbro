using System.Text.RegularExpressions;
using BuildingBlocks.Application.Authorization;
using Xunit;

namespace Gymbro.Tests.Authorization;

public sealed class TenantScopedRequestConventionTests
{
    [Fact]
    public void Discovered_authorized_controller_requests_are_classified()
    {
        var discovered = AuthorizedControllerRequestDiscovery.DiscoverRequestTypes();
        Assert.NotEmpty(discovered);

        var unclassified = discovered
            .Where(t => !typeof(ITenantAuthorizedRequest).IsAssignableFrom(t))
            .Where(t => !typeof(IPlatformAdminRequest).IsAssignableFrom(t))
            .Where(t => !TenantAuthorizationExemptions.IsExempt(t.Name))
            .Select(t => t.Name)
            .ToList();

        Assert.True(
            unclassified.Count == 0,
            "Each authorized-controller MediatR request must implement ITenantAuthorizedRequest "
            + "or IPlatformAdminRequest, or be listed in TenantAuthorizationExemptions: "
            + string.Join(", ", unclassified));
    }

    [Fact]
    public void Declarative_requests_do_not_appear_on_imperative_exemption_list()
    {
        var declarative = AuthorizedControllerRequestDiscovery.DiscoverRequestTypes()
            .Where(t => typeof(ITenantAuthorizedRequest).IsAssignableFrom(t)
                || typeof(IPlatformAdminRequest).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        var onExemption = declarative
            .Where(TenantAuthorizationExemptions.IsExempt)
            .ToList();

        Assert.True(
            onExemption.Count == 0,
            "Types implementing ITenantAuthorizedRequest or IPlatformAdminRequest "
            + "must be removed from TenantAuthorizationExemptions: "
            + string.Join(", ", onExemption));
    }

    /// <summary>
    /// The exemption set is a manually-maintained seam: each entry must say why
    /// it bypasses declarative gating, so a future addition can't slip in undocumented.
    /// </summary>
    [Fact]
    public void Every_exemption_documents_a_reason()
    {
        var undocumented = TenantAuthorizationExemptions.All
            .Where(e => string.IsNullOrWhiteSpace(e.Value.Reason) || e.Value.Reason.Trim().Length < 20)
            .Select(e => e.Key)
            .ToList();

        Assert.True(
            undocumented.Count == 0,
            "Every TenantAuthorizationExemptions entry must carry a meaningful Reason explaining why it "
            + "bypasses declarative authorization: " + string.Join(", ", undocumented));
    }

    /// <summary>
    /// The static <c>ITenantAuthorizedRequest</c> gate can't express row-level rules, so
    /// <see cref="ExemptionKind.ImperativeGuarded"/> handlers enforce access themselves. This catches a
    /// handler that forgets the guard: its source must reference an authorization primitive. Pairs with
    /// the integration tests that attempt cross-trainee reads through these endpoints
    /// (see <c>Integration/CrossTraineeAccessTests</c>).
    /// </summary>
    [Fact]
    public void Imperative_guarded_handlers_reference_an_authorization_check()
    {
        string[] authPrimitives =
        [
            "HasPermissionAsync",
            "CanAccessResourceAsync",
            "ResourceAccessGuard",
            "currentUser.UserId",
            "currentUser.IsAdmin",
        ];

        var unguarded = new List<string>();

        foreach (var (requestName, exemption) in TenantAuthorizationExemptions.All)
        {
            if (exemption.Kind != ExemptionKind.ImperativeGuarded)
                continue;

            var handlerFile = LocateHandlerSource(requestName);
            if (handlerFile is null)
            {
                unguarded.Add($"{requestName} (handler source not found)");
                continue;
            }

            var source = File.ReadAllText(handlerFile);
            if (!authPrimitives.Any(p => source.Contains(p, StringComparison.Ordinal)))
                unguarded.Add($"{requestName} (no authorization primitive in {Path.GetFileName(handlerFile)})");
        }

        Assert.True(
            unguarded.Count == 0,
            "Each ImperativeGuarded exemption's handler must enforce access with one of "
            + $"[{string.Join(", ", authPrimitives)}]. Missing: {string.Join("; ", unguarded)}");
    }

    /// <summary>
    /// <see cref="ExemptionKind.InternalLookup"/> requests carry no caller-facing authorization — they are
    /// safe only because they are reached in-process behind an already-guarded handler, never dispatched
    /// from a controller. This asserts that contract: if one is ever referenced in a
    /// controller, it must instead be given its own declarative gate or row-level guard.
    /// </summary>
    [Fact]
    public void InternalLookup_requests_are_never_dispatched_from_a_controller()
    {
        var internalOnly = TenantAuthorizationExemptions.All
            .Where(e => e.Value.Kind == ExemptionKind.InternalLookup)
            .Select(e => e.Key)
            .ToList();

        Assert.NotEmpty(internalOnly);

        var leaks = new List<string>();
        foreach (var file in AuthorizedControllerRequestDiscovery.ControllerFiles())
        {
            var source = File.ReadAllText(file);
            foreach (var name in internalOnly)
                if (Regex.IsMatch(source, $@"\b{Regex.Escape(name)}\b"))
                    leaks.Add($"{name} → {Path.GetFileName(file)}");
        }

        Assert.True(
            leaks.Count == 0,
            "InternalLookup exemptions must never be dispatched from a controller (they have no "
            + "caller-facing authorization). Give them a declarative/row-level guard before exposing: "
            + string.Join("; ", leaks));
    }

    /// <summary>
    /// Resolves a request name (e.g. <c>ListSessionsQuery</c>) to its handler source file
    /// (<c>ListSessionsHandler.cs</c>) by convention, searching the Modules tree.
    /// </summary>
    private static string? LocateHandlerSource(string requestName)
    {
        var baseName = requestName;
        foreach (var suffix in new[] { "Query", "Command" })
            if (baseName.EndsWith(suffix, StringComparison.Ordinal))
            {
                baseName = baseName[..^suffix.Length];
                break;
            }

        var handlerFileName = baseName + "Handler.cs";
        var modulesRoot = LocateModulesRoot();

        return Directory
            .EnumerateFiles(modulesRoot, handlerFileName, SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal));
    }

    private static string LocateModulesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Modules");
            if (Directory.Exists(candidate))
                return candidate;

            candidate = Path.Combine(dir.FullName, "gymbro", "Modules");
            if (Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the Modules directory for tenant authorization convention tests.");
    }
}
