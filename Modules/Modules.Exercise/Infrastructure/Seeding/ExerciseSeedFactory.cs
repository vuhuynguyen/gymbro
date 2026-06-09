using BuildingBlocks.Shared.Tracking;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Infrastructure.Seeding;

/// <summary>
/// Maps a validated <see cref="ExerciseSeedDto"/> onto the <see cref="Exercise"/> aggregate, using only the
/// domain factory and its <c>Replace*</c> methods (no reflection, no schema bypass). Supports both creating a
/// new global exercise and applying the seed to an existing one in place (preserving its Id so workout
/// plans/logs that reference it stay valid).
///
/// <para>Persisted: name, description, type, trackingType, primary/secondary muscles, equipment, difficulty,
/// mechanics (→ MovementType), calories, duration, instructions, safety notes (→ warnings), and the merged
/// search tags. Not persisted by the current schema (see <see cref="ExerciseSeedDto"/>): slug, aliases,
/// commonMistakes, and the structured category/movementPattern/forceType/equipmentDetail — the latter four are
/// folded into the search tags so the catalog stays searchable by them.</para>
/// </summary>
public static class ExerciseSeedFactory
{
    /// <summary>Creates a new global catalog exercise from the seed entry.</summary>
    public static Exercise Create(ExerciseSeedDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var exercise = Exercise.CreateGlobal(
            name: dto.Name!.Trim(),
            imageUrl: string.Empty, // media gap — no copyrighted media seeded; see docs/master-data/MEDIA_STRATEGY.md
            description: dto.Description ?? string.Empty,
            type: ParseType(dto.Type),
            movementType: ParseMechanics(dto.Mechanics),
            difficulty: ParseDifficulty(dto.Difficulty),
            equipment: ParseEquipment(dto.Equipment),
            estimatedCaloriesBurn: dto.EstimatedCalories,
            averageDurationSeconds: dto.AverageDurationSeconds,
            muscles: BuildMuscles(dto),
            trackingType: ResolveTrackingType(dto));

        ApplyChildCollections(exercise, dto);
        return exercise;
    }

    /// <summary>Applies the seed entry onto an existing tracked exercise in place (keeps its Id).</summary>
    public static void Apply(Exercise existing, ExerciseSeedDto dto)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(dto);

        existing.UpdateCatalog(
            name: dto.Name!.Trim(),
            description: dto.Description ?? string.Empty,
            imageUrl: string.Empty,
            type: ParseType(dto.Type),
            movementType: ParseMechanics(dto.Mechanics),
            difficulty: ParseDifficulty(dto.Difficulty),
            equipment: ParseEquipment(dto.Equipment),
            estimatedCaloriesBurn: dto.EstimatedCalories,
            averageDurationSeconds: dto.AverageDurationSeconds,
            trackingType: ResolveTrackingType(dto));

        existing.ReplaceMuscles(BuildMuscles(dto));
        ApplyChildCollections(existing, dto);
    }

    private static void ApplyChildCollections(Exercise exercise, ExerciseSeedDto dto)
    {
        exercise.ReplaceInstructions(dto.Instructions);
        exercise.ReplaceTags(BuildTags(dto));
        exercise.ReplaceWarnings(dto.SafetyNotes);
    }

    private static IReadOnlyList<(MuscleGroup muscle, bool isPrimary)> BuildMuscles(ExerciseSeedDto dto)
    {
        var muscles = new List<(MuscleGroup, bool)> { (ParseMuscle(dto.PrimaryMuscle), true) };
        foreach (var s in dto.SecondaryMuscles)
        {
            var muscle = ParseMuscle(s);
            if (muscles.All(m => m.Item1 != muscle))
                muscles.Add((muscle, false));
        }
        return muscles;
    }

    /// <summary>
    /// Merges the freeform tags with the structured-but-not-yet-persisted facets (category, movement pattern,
    /// force type, equipment detail) so the catalog remains searchable by them. <c>ReplaceTags</c> trims and
    /// de-duplicates case-insensitively, so order/overlap here is harmless.
    /// </summary>
    private static List<string> BuildTags(ExerciseSeedDto dto)
    {
        var tags = new List<string>(dto.Tags);
        AddSlugified(tags, dto.Category);
        AddSlugified(tags, dto.MovementPattern);
        AddSlugified(tags, dto.ForceType);
        AddSlugified(tags, dto.EquipmentDetail);
        return tags;
    }

    private static void AddSlugified(List<string> tags, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        // "Horizontal Push" → "horizontal-push"; "Cardio Machine" → "cardio-machine".
        var slug = value.Trim().ToLowerInvariant().Replace(' ', '-').Replace("/", "-");
        if (!string.IsNullOrWhiteSpace(slug))
            tags.Add(slug);
    }

    private static ExerciseTrackingType ResolveTrackingType(ExerciseSeedDto dto) =>
        !string.IsNullOrWhiteSpace(dto.TrackingType)
            ? Enum.Parse<ExerciseTrackingType>(dto.TrackingType.Trim(), ignoreCase: true)
            : ExerciseTrackingDefaults.Derive(ParseType(dto.Type), ParseEquipment(dto.Equipment));

    private static ExerciseType ParseType(string? v) => Enum.Parse<ExerciseType>(v!.Trim(), ignoreCase: true);
    private static MovementType ParseMechanics(string? v) => Enum.Parse<MovementType>(v!.Trim(), ignoreCase: true);
    private static DifficultyLevel ParseDifficulty(string? v) => Enum.Parse<DifficultyLevel>(v!.Trim(), ignoreCase: true);
    private static Equipment ParseEquipment(string? v) => Enum.Parse<Equipment>(v!.Trim(), ignoreCase: true);
    private static MuscleGroup ParseMuscle(string? v) => Enum.Parse<MuscleGroup>(v!.Trim(), ignoreCase: true);
}
