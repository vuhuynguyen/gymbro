using System.Reflection;
using System.Text.Json;

namespace Modules.FoodModule.Infrastructure.Seeding;

/// <summary>
/// Loads the food master-data seed file. The JSON ships as an <b>embedded resource</b> in this assembly so
/// seeding is deployment-safe (no working-directory/content-file assumptions in containers). Pure I/O +
/// parsing — no database dependency — so it lives in the Food module and is reused by the WebApi seeder and by
/// tests. Mirrors <c>ExerciseSeedDataLoader</c>.
/// </summary>
public sealed class FoodSeedDataLoader
{
    private static readonly Assembly Assembly = typeof(FoodSeedDataLoader).Assembly;

    // JsonSerializerDefaults.Web → camelCase + case-insensitive property matching (matches the API wire format).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FoodSeedData Load()
    {
        var file = Deserialize<FoodSeedFile>("foods.json");
        if (file?.Foods is null)
            throw new FoodSeedDataException("foods.json did not contain a 'foods' array.");

        return new FoodSeedData(file.Foods);
    }

    private static T Deserialize<T>(string fileName)
    {
        using var stream = OpenResource(fileName);
        try
        {
            var value = JsonSerializer.Deserialize<T>(stream, JsonOptions);
            return value ?? throw new FoodSeedDataException($"{fileName} deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new FoodSeedDataException($"{fileName} is not valid JSON: {ex.Message}", ex);
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
            throw new FoodSeedDataException(
                $"Embedded seed resource '{fileName}' not found. Ensure it is included as an <EmbeddedResource> " +
                "in Modules.Food.csproj (Infrastructure/SeedData/*.json).");

        return Assembly.GetManifestResourceStream(resourceName)
               ?? throw new FoodSeedDataException($"Could not open embedded seed resource '{resourceName}'.");
    }
}

/// <summary>Thrown when the seed file cannot be loaded or is structurally invalid. Surfaces a clear, fail-fast error.</summary>
public sealed class FoodSeedDataException : Exception
{
    public FoodSeedDataException(string message) : base(message) { }
    public FoodSeedDataException(string message, Exception inner) : base(message, inner) { }
}
