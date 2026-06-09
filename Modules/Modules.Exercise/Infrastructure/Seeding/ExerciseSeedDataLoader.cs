using System.Reflection;
using System.Text.Json;

namespace Modules.ExerciseModule.Infrastructure.Seeding;

/// <summary>
/// Loads the exercise master-data seed files. The JSON ships as <b>embedded resources</b> in this assembly so
/// seeding is deployment-safe (no working-directory/content-file assumptions in containers). Pure I/O + parsing
/// — no database dependency, so it lives in the Exercise module and is reused by the WebApi seeder and by tests.
/// </summary>
public sealed class ExerciseSeedDataLoader
{
    private static readonly Assembly Assembly = typeof(ExerciseSeedDataLoader).Assembly;

    // JsonSerializerDefaults.Web → camelCase + case-insensitive property matching (matches the API wire format).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Loads and deserializes all four seed files. Throws <see cref="ExerciseSeedDataException"/> if a
    /// resource is missing or malformed (fail fast — never seed from a half-read file).</summary>
    public ExerciseSeedData Load()
    {
        var file = Deserialize<ExerciseSeedFile>("exercises.json");
        if (file?.Exercises is null)
            throw new ExerciseSeedDataException("exercises.json did not contain an 'exercises' array.");

        return new ExerciseSeedData(
            file.Exercises,
            LoadCodes("muscles.json", "muscles"),
            LoadCodes("equipment.json", "equipment"),
            LoadCodes("categories.json", "categories"));
    }

    private static T Deserialize<T>(string fileName)
    {
        using var stream = OpenResource(fileName);
        try
        {
            var value = JsonSerializer.Deserialize<T>(stream, JsonOptions);
            return value ?? throw new ExerciseSeedDataException($"{fileName} deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new ExerciseSeedDataException($"{fileName} is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>Reads the <c>code</c> of every object in the named array property of a lookup file
    /// (case-insensitive set). Tolerates the leading <c>$comment</c>/metadata properties.</summary>
    private static IReadOnlySet<string> LoadCodes(string fileName, string arrayProperty)
    {
        using var stream = OpenResource(fileName);
        try
        {
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty(arrayProperty, out var array) ||
                array.ValueKind != JsonValueKind.Array)
                throw new ExerciseSeedDataException($"{fileName} is missing the '{arrayProperty}' array.");

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in array.EnumerateArray())
            {
                if (item.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
                    codes.Add(code.GetString()!);
            }

            if (codes.Count == 0)
                throw new ExerciseSeedDataException($"{fileName} '{arrayProperty}' contained no codes.");

            return codes;
        }
        catch (JsonException ex)
        {
            throw new ExerciseSeedDataException($"{fileName} is not valid JSON: {ex.Message}", ex);
        }
    }

    private static Stream OpenResource(string fileName)
    {
        // Embedded resource name is "<RootNamespace>.Infrastructure.SeedData.<file>"; match by suffix to stay
        // robust to namespace/folder changes.
        var suffix = $".Infrastructure.SeedData.{fileName}";
        var resourceName = Assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new ExerciseSeedDataException(
                $"Embedded seed resource '{fileName}' not found. Ensure it is included as an <EmbeddedResource> " +
                "in Modules.Exercise.csproj (Infrastructure/SeedData/*.json).");

        return Assembly.GetManifestResourceStream(resourceName)
               ?? throw new ExerciseSeedDataException($"Could not open embedded seed resource '{resourceName}'.");
    }
}

/// <summary>Thrown when seed files cannot be loaded or are structurally invalid. Surfaces a clear, fail-fast error.</summary>
public sealed class ExerciseSeedDataException : Exception
{
    public ExerciseSeedDataException(string message) : base(message) { }
    public ExerciseSeedDataException(string message, Exception inner) : base(message, inner) { }
}
