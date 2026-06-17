namespace Modules.ExerciseModule.Infrastructure.Seeding;

/// <summary>
/// Root of <c>exercises.json</c>. Seed master data is authored in structured files (never hardcoded in C#);
/// this is the deserialization shape. See <see cref="ExerciseSeedDataLoader"/> and <c>docs/SEEDING.md</c>.
/// </summary>
public sealed class ExerciseSeedFile
{
    public int Version { get; set; }
    public List<ExerciseSeedDto> Exercises { get; set; } = [];
}

/// <summary>
/// One exercise as authored in the seed file. The shape is intentionally richer than the current schema so the
/// files stay forward-compatible (see the architecture proposal under docs/master-data/). Fields that the
/// current schema cannot persist as structured columns are documented on each property and are either folded
/// into search <see cref="Tags"/> or preserved for a future migration — never silently dropped without a record.
/// </summary>
public sealed class ExerciseSeedDto
{
    /// <summary>Stable, language-neutral identity/dedup key (e.g. <c>barbell-bench-press</c>).
    /// NOT persisted (no Slug column yet) — used for validation/dedup and reserved for the future schema.</summary>
    public string? Slug { get; set; }

    /// <summary>Canonical display name → persisted as <c>Exercise.DefaultName</c> (also the upsert match key).</summary>
    public string? Name { get; set; }

    /// <summary>→ persisted as <c>Exercise.DefaultDescription</c>.</summary>
    public string? Description { get; set; }

    /// <summary>Fine-grained library category (e.g. <c>biceps</c>) — validated against categories.json.
    /// NOT a structured column; folded into search <see cref="Tags"/>.</summary>
    public string? Category { get; set; }

    /// <summary>ExerciseType enum (Strength/Cardio/Mobility/Stretching) → persisted.</summary>
    public string? Type { get; set; }

    /// <summary>Optional ExerciseTrackingType override (Timed/Hiit/Custom/…); derived from Type+Equipment when null.</summary>
    public string? TrackingType { get; set; }

    /// <summary>Primary MuscleGroup enum → persisted (IsPrimary = true).</summary>
    public string? PrimaryMuscle { get; set; }

    /// <summary>Secondary MuscleGroup enums → persisted (IsPrimary = false).</summary>
    public List<string> SecondaryMuscles { get; set; } = [];

    /// <summary>Equipment enum (Bodyweight/Barbell/Dumbbell/Machine/ResistanceBand) → persisted.</summary>
    public string? Equipment { get; set; }

    /// <summary>Real-world equipment (e.g. Kettlebell, Cable, Cardio Machine) mapped onto the closest
    /// <see cref="Equipment"/> code. NOT a structured column; folded into search <see cref="Tags"/>.</summary>
    public string? EquipmentDetail { get; set; }

    /// <summary>DifficultyLevel enum → persisted.</summary>
    public string? Difficulty { get; set; }

    /// <summary>Mechanics → persisted as <c>Exercise.MovementType</c> (Compound/Isolation).</summary>
    public string? Mechanics { get; set; }

    /// <summary>Movement pattern (e.g. Horizontal Push, Hinge). NOT a structured column; folded into search <see cref="Tags"/>.</summary>
    public string? MovementPattern { get; set; }

    /// <summary>Force type (Push/Pull/Static). NOT a structured column; folded into search <see cref="Tags"/>.</summary>
    public string? ForceType { get; set; }

    /// <summary>→ persisted as <c>Exercise.EstimatedCaloriesBurn</c> (nullable, estimate only).</summary>
    public int? EstimatedCalories { get; set; }

    /// <summary>→ persisted as <c>Exercise.AverageDurationSeconds</c>.</summary>
    public int? AverageDurationSeconds { get; set; }

    /// <summary>Ordered instruction steps → persisted as <c>ExerciseInstruction</c> rows.</summary>
    public List<string> Instructions { get; set; } = [];

    /// <summary>Safety/caution notes → persisted as <c>ExerciseWarning</c> rows.</summary>
    public List<string> SafetyNotes { get; set; } = [];

    /// <summary>Common technique mistakes. NOT persisted (no column yet) — preserved for the future schema.</summary>
    public List<string> CommonMistakes { get; set; } = [];

    /// <summary>Search synonyms/aliases. NOT persisted as structured rows yet; reserved for the future schema.
    /// (Folding these into freeform tags would pollute the tag taxonomy, so they are intentionally kept separate.)</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>Extra freeform search tags → merged with category/movementPattern/forceType/equipmentDetail and persisted as <c>ExerciseTag</c> rows.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Active catalog entry. <c>false</c> → skipped on seed / soft-deleted on reseed. Defaults to true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Specific (fine) primary muscle slugs for the activation map (e.g. <c>["hamstring"]</c>).</summary>
    public List<string> DetailedPrimaryMuscles { get; set; } = [];

    /// <summary>Specific (fine) secondary muscle slugs.</summary>
    public List<string> DetailedSecondaryMuscles { get; set; } = [];

    /// <summary>Image URL (e.g. a CDN link). Persisted as <c>Exercise.ImageUrl</c>.</summary>
    public string? ImageUrl { get; set; }
}

/// <summary>Loaded, deserialized seed inputs: the exercises plus the valid lookup-code sets used for validation.</summary>
public sealed record ExerciseSeedData(
    IReadOnlyList<ExerciseSeedDto> Exercises,
    IReadOnlySet<string> ValidMuscleCodes,
    IReadOnlySet<string> ValidEquipmentCodes,
    IReadOnlySet<string> ValidCategoryCodes);
