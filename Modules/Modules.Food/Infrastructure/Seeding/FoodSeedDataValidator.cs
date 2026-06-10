using Modules.FoodModule.Entities;

namespace Modules.FoodModule.Infrastructure.Seeding;

/// <summary>
/// Validates the loaded seed set <b>before any database write</b>. Collects every problem (rather than throwing
/// on the first) so an author can fix the file in one pass. The caller fails fast — no partial import — when
/// <see cref="FoodSeedValidationResult.IsValid"/> is false. Mirrors <c>ExerciseSeedDataValidator</c>.
/// </summary>
public sealed class FoodSeedDataValidator
{
    public FoodSeedValidationResult Validate(FoodSeedData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var errors = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data.Foods.Count == 0)
            errors.Add("Seed set contains no foods.");

        for (var i = 0; i < data.Foods.Count; i++)
        {
            var f = data.Foods[i];
            var id = !string.IsNullOrWhiteSpace(f.Name) ? f.Name! : $"#index {i}";
            void Err(string msg) => errors.Add($"[{id}] {msg}");

            // ── Required fields ──
            Require(f.Name, "name", Err);
            Require(f.Kind, "kind", Err);
            Require(f.ServingLabel, "servingLabel", Err);

            // ── Uniqueness ──
            if (!string.IsNullOrWhiteSpace(f.Name) && !seenNames.Add(f.Name.Trim()))
                Err($"duplicate canonical name '{f.Name}'.");

            // ── Enum validity ──
            if (!string.IsNullOrWhiteSpace(f.Kind) && !Enum.TryParse<FoodKind>(f.Kind.Trim(), ignoreCase: true, out _))
                Err($"'kind' value '{f.Kind}' is not a valid FoodKind " +
                    $"(allowed: {string.Join(", ", Enum.GetNames<FoodKind>())}).");

            // ── Numeric sanity (mirrors the Food aggregate's GuardMacros) ──
            if (f.ServingSizeGrams is <= 0) Err("servingSizeGrams must be positive when present.");
            NonNegative(f.EnergyKcal, "energyKcal", Err);
            NonNegative(f.ProteinG, "proteinG", Err);
            NonNegative(f.CarbsG, "carbsG", Err);
            NonNegative(f.FatG, "fatG", Err);
            NonNegative(f.FiberG, "fiberG", Err);
        }

        return new FoodSeedValidationResult(errors);
    }

    private static void Require(string? value, string field, Action<string> err)
    {
        if (string.IsNullOrWhiteSpace(value))
            err($"required field '{field}' is missing.");
    }

    private static void NonNegative(decimal? value, string field, Action<string> err)
    {
        if (value is < 0)
            err($"'{field}' cannot be negative.");
    }
}

/// <summary>Outcome of seed validation: the (possibly empty) list of human-readable error messages.</summary>
public sealed class FoodSeedValidationResult(IReadOnlyList<string> errors)
{
    public IReadOnlyList<string> Errors { get; } = errors;
    public bool IsValid => Errors.Count == 0;
}
