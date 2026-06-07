using System.Reflection;
using System.Text.RegularExpressions;
using MediatR;

namespace Gymbro.Tests.Authorization;

/// <summary>
/// Discovers MediatR request types dispatched from Web API controllers that require authorization metadata.
/// Uses controller source scanning (not a full request registry) plus type resolution across module assemblies.
/// </summary>
internal static class AuthorizedControllerRequestDiscovery
{
    private static readonly Regex NewRequestRegex = new(
        @"\bnew\s+(\w+(?:Command|Query))\s*[\(\{]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FromBodyRequestRegex = new(
        @"\[FromBody\]\s+(\w+(?:Command|Query))\s+\w+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FromQueryRequestRegex = new(
        @"\[FromQuery\]\s+(\w+(?:Command|Query))\s+\w+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // No controllers are skipped: AuthController is now scanned too, so a
    // tenant-scoped request added to it cannot escape classification. Its anonymous / self-scoped
    // requests are documented as AuthenticationOnly in TenantAuthorizationExemptions.
    private static readonly string[] SkippedControllerFiles = [];

    public static IReadOnlyList<Type> DiscoverRequestTypes()
    {
        var controllersPath = LocateControllersDirectory();
        var requestNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(controllersPath, "*Controller.cs"))
        {
            var fileName = Path.GetFileName(file);
            if (SkippedControllerFiles.Contains(fileName, StringComparer.Ordinal))
                continue;

            var source = File.ReadAllText(file);
            if (!RequiresAuthorizationMetadata(source, fileName))
                continue;

            foreach (Match match in NewRequestRegex.Matches(source))
                requestNames.Add(match.Groups[1].Value);

            foreach (Match match in FromBodyRequestRegex.Matches(source))
                requestNames.Add(match.Groups[1].Value);

            foreach (Match match in FromQueryRequestRegex.Matches(source))
                requestNames.Add(match.Groups[1].Value);
        }

        return requestNames
            .Select(ResolveRequestType)
            .Where(t => t is not null)
            .Cast<Type>()
            .Distinct()
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private static bool RequiresAuthorizationMetadata(string source, string fileName)
    {
        if (source.Contains("[Authorize]", StringComparison.Ordinal))
            return true;

        // Admin and tenant-scoped controllers are always authenticated even if only methods are attributed.
        return fileName is "AdminController.cs"
            or "ExerciseController.cs"
            or "WorkoutPlanController.cs"
            or "SessionController.cs"
            or "InviteController.cs"
            or "UserController.cs";
    }

    private static Type? ResolveRequestType(string typeName)
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
            if (type is not null && ImplementsMediatRRequest(type))
                return type;
        }

        return null;
    }

    private static bool ImplementsMediatRRequest(Type type)
    {
        if (type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            return true;

        return type.GetInterfaces().Any(i => i == typeof(IRequest));
    }

    private static IReadOnlyList<Assembly> ModuleAssemblies { get; } =
    [
        typeof(Modules.UserModule.Application.Commands.CreateTenantCommand).Assembly,
        typeof(Modules.IdentityModule.Application.Commands.LoginCommand).Assembly,
        typeof(Modules.ExerciseModule.Application.Commands.CreateExerciseCommand).Assembly,
        typeof(Modules.WorkoutPlanModule.Application.Commands.CreateWorkoutPlanCommand).Assembly,
        typeof(Modules.WorkoutSessionModule.Application.Commands.StartSessionCommand).Assembly,
    ];

    /// <summary>Absolute paths of every <c>*Controller.cs</c> source file (for convention tests that
    /// scan controller bodies, e.g. asserting InternalLookup requests are never dispatched).</summary>
    internal static IReadOnlyList<string> ControllerFiles() =>
        Directory.EnumerateFiles(LocateControllersDirectory(), "*Controller.cs").ToList();

    internal static string LocateControllersDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Presentations", "WebApi", "Controllers");
            if (Directory.Exists(candidate))
                return candidate;

            candidate = Path.Combine(dir.FullName, "gymbro", "Presentations", "WebApi", "Controllers");
            if (Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate WebApi/Controllers for tenant authorization convention tests.");
    }
}
