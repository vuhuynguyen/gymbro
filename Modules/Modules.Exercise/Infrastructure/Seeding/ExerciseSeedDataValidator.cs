using BuildingBlocks.Shared.Tracking;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Infrastructure.Seeding;

/// <summary>
/// Validates the loaded seed set <b>before any database write</b>. Collects every problem and returns them all
/// (rather than throwing on the first) so an author can fix the file in one pass. The caller fails fast — no
/// partial import — when <see cref="ExerciseSeedValidationResult.IsValid"/> is false.
/// </summary>
public sealed class ExerciseSeedDataValidator
{
    public ExerciseSeedValidationResult Validate(ExerciseSeedData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var errors = new List<string>();
        var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allNames = data.Exercises
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => e.Name!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (data.Exercises.Count == 0)
            errors.Add("Seed set contains no exercises.");

        for (var i = 0; i < data.Exercises.Count; i++)
        {
            var e = data.Exercises[i];
            // Prefer the slug as the error label; fall back to name/index.
            var id = !string.IsNullOrWhiteSpace(e.Slug) ? e.Slug!
                : !string.IsNullOrWhiteSpace(e.Name) ? e.Name!
                : $"#index {i}";

            void Err(string msg) => errors.Add($"[{id}] {msg}");

            // ── Required fields ──────────────────────────────────────────────────────────
            Require(e.Slug, "slug", Err);
            Require(e.Name, "name", Err);
            Require(e.Description, "description", Err);
            Require(e.Category, "category", Err);
            Require(e.Type, "type", Err);
            Require(e.PrimaryMuscle, "primaryMuscle", Err);
            Require(e.Equipment, "equipment", Err);
            Require(e.Difficulty, "difficulty", Err);
            Require(e.Mechanics, "mechanics", Err);

            // ── Uniqueness ───────────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(e.Slug) && !seenSlugs.Add(e.Slug.Trim()))
                Err($"duplicate slug '{e.Slug}'.");
            if (!string.IsNullOrWhiteSpace(e.Name) && !seenNames.Add(e.Name.Trim()))
                Err($"duplicate canonical name '{e.Name}'.");

            // ── Enum / lookup validity ───────────────────────────────────────────────────
            ValidateEnum<ExerciseType>(e.Type, "type", Err);
            ValidateEnum<MovementType>(e.Mechanics, "mechanics", Err);
            ValidateEnum<DifficultyLevel>(e.Difficulty, "difficulty", Err);
            if (!string.IsNullOrWhiteSpace(e.TrackingType))
                ValidateEnum<ExerciseTrackingType>(e.TrackingType, "trackingType", Err);

            ValidateLookup(e.Category, data.ValidCategoryCodes, "category", Err);
            ValidateLookup(e.Equipment, data.ValidEquipmentCodes, "equipment", Err);
            if (!string.IsNullOrWhiteSpace(e.Equipment))
                ValidateEnum<Equipment>(e.Equipment, "equipment", Err);

            // ── Muscles ──────────────────────────────────────────────────────────────────
            ValidateLookup(e.PrimaryMuscle, data.ValidMuscleCodes, "primaryMuscle", Err);
            if (!string.IsNullOrWhiteSpace(e.PrimaryMuscle))
                ValidateEnum<MuscleGroup>(e.PrimaryMuscle, "primaryMuscle", Err);

            var secondarySeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in e.SecondaryMuscles)
            {
                ValidateLookup(s, data.ValidMuscleCodes, "secondaryMuscles", Err);
                ValidateEnum<MuscleGroup>(s, "secondaryMuscles", Err);
                if (!secondarySeen.Add(s?.Trim() ?? ""))
                    Err($"duplicate secondary muscle '{s}'.");
                if (!string.IsNullOrWhiteSpace(e.PrimaryMuscle) &&
                    string.Equals(s?.Trim(), e.PrimaryMuscle.Trim(), StringComparison.OrdinalIgnoreCase))
                    Err($"secondary muscle '{s}' duplicates the primary muscle.");
            }

            // ── Instructions ─────────────────────────────────────────────────────────────
            if (e.Instructions.Count == 0)
                Err("at least one instruction step is required.");
            else if (e.Instructions.Any(string.IsNullOrWhiteSpace))
                Err("instructions contain an empty step.");

            // ── Aliases (no duplicates within the exercise; globally unique; not equal to any name) ──
            var aliasSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in e.Aliases)
            {
                if (string.IsNullOrWhiteSpace(a)) { Err("alias is empty."); continue; }
                var alias = a.Trim();
                if (!aliasSeen.Add(alias))
                    Err($"duplicate alias '{alias}' within the exercise.");
                if (!seenAliases.Add(alias))
                    Err($"alias '{alias}' is already used by another exercise (aliases must be globally unique).");
                if (allNames.Contains(alias) &&
                    !string.Equals(alias, e.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                    Err($"alias '{alias}' collides with another exercise's canonical name.");
            }

            // ── Numeric sanity ───────────────────────────────────────────────────────────
            if (e.EstimatedCalories is < 0)
                Err("estimatedCalories cannot be negative.");
            if (e.AverageDurationSeconds is < 0)
                Err("averageDurationSeconds cannot be negative.");
        }

        return new ExerciseSeedValidationResult(errors);
    }

    private static void Require(string? value, string field, Action<string> err)
    {
        if (string.IsNullOrWhiteSpace(value))
            err($"required field '{field}' is missing.");
    }

    private static void ValidateEnum<TEnum>(string? value, string field, Action<string> err)
        where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value) && !Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out _))
            err($"'{field}' value '{value}' is not a valid {typeof(TEnum).Name} " +
                $"(allowed: {string.Join(", ", Enum.GetNames<TEnum>())}).");
    }

    private static void ValidateLookup(string? value, IReadOnlySet<string> valid, string field, Action<string> err)
    {
        if (!string.IsNullOrWhiteSpace(value) && !valid.Contains(value.Trim()))
            err($"'{field}' value '{value}' is not a known code " +
                $"(allowed: {string.Join(", ", valid.OrderBy(x => x))}).");
    }
}

/// <summary>Outcome of seed validation: the (possibly empty) list of human-readable error messages.</summary>
public sealed class ExerciseSeedValidationResult(IReadOnlyList<string> errors)
{
    public IReadOnlyList<string> Errors { get; } = errors;
    public bool IsValid => Errors.Count == 0;
}
