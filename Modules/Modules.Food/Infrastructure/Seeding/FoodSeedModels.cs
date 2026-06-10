namespace Modules.FoodModule.Infrastructure.Seeding;

/// <summary>
/// Root of <c>foods.json</c>. Food master data is authored in a structured file (never hardcoded in C#); this
/// is the deserialization shape. See <see cref="FoodSeedDataLoader"/> and <c>docs/nutrition/FOOD_SEEDING.md</c>.
/// </summary>
public sealed class FoodSeedFile
{
    public int Version { get; set; }
    public List<FoodSeedDto> Foods { get; set; } = [];
}

/// <summary>
/// One catalog food as authored in the seed file. Macros are PER the stated serving (not per 100 g). Every
/// field maps directly to a <c>Food</c> column — the schema is flat (no children), so unlike the exercise seed
/// nothing is folded or deferred.
/// </summary>
public sealed class FoodSeedDto
{
    /// <summary>Canonical display name → <c>Food.Name</c> (also the upsert match key, so it must be unique).</summary>
    public string? Name { get; set; }

    /// <summary>Optional brand → <c>Food.Brand</c>.</summary>
    public string? Brand { get; set; }

    /// <summary>FoodKind enum (Food / Supplement / Beverage) → <c>Food.Kind</c>.</summary>
    public string? Kind { get; set; }

    /// <summary>Human label for the canonical serving (e.g. "1 scoop", "100 g") → <c>Food.ServingLabel</c>.</summary>
    public string? ServingLabel { get; set; }

    /// <summary>Mass of the serving in grams, when known → <c>Food.ServingSizeGrams</c>. Null for count-based items.</summary>
    public decimal? ServingSizeGrams { get; set; }

    // Headline macros PER serving → the matching nullable Food columns.
    public decimal? EnergyKcal { get; set; }
    public decimal? ProteinG { get; set; }
    public decimal? CarbsG { get; set; }
    public decimal? FatG { get; set; }
    public decimal? FiberG { get; set; }

    /// <summary>Active catalog entry. <c>false</c> → skipped on seed / soft-deleted on reseed. Defaults to true.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>Loaded, deserialized seed inputs.</summary>
public sealed record FoodSeedData(IReadOnlyList<FoodSeedDto> Foods);
