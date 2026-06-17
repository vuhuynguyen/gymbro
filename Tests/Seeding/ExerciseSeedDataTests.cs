using System.Linq;
using BuildingBlocks.Shared.Tracking;
using Modules.ExerciseModule.Entities;
using Modules.ExerciseModule.Infrastructure.Seeding;
using Xunit;

namespace Gymbro.Tests.Seeding;

/// <summary>
/// Guards the exercise master-data seed pipeline (loader → validator → factory). These are pure (no database),
/// so they run everywhere and act as a regression guard on the embedded seed files themselves: if someone adds
/// an exercise with a bad enum, duplicate slug, empty instruction, or duplicate alias, the build's tests fail
/// before it can reach the database.
/// </summary>
public sealed class ExerciseSeedDataTests
{
    private static ExerciseSeedData Load() => new ExerciseSeedDataLoader().Load();

    [Fact]
    public void Seed_files_load_with_a_production_sized_library()
    {
        var data = Load();

        Assert.True(data.Exercises.Count >= 50,
            $"Expected a production starter library (>= 50 exercises); found {data.Exercises.Count}.");
        Assert.Contains("Chest", data.ValidMuscleCodes);
        Assert.Contains("Barbell", data.ValidEquipmentCodes);
        Assert.Contains("biceps", data.ValidCategoryCodes);
    }

    [Fact]
    public void Embedded_seed_data_passes_validation()
    {
        var result = new ExerciseSeedDataValidator().Validate(Load());

        Assert.True(result.IsValid,
            "Embedded seed data failed validation:\n" + string.Join("\n", result.Errors));
    }

    /// <summary>The 16 fine muscle slugs the activation-map renderer understands (front + back anatomy).
    /// Both clients (Flutter <c>muscle_map.dart</c>, Angular <c>muscle-map-paths.ts</c>) carry the same set —
    /// a slug outside this list would silently render nothing on the map.</summary>
    private static readonly System.Collections.Generic.HashSet<string> KnownFineMuscles =
        ("chest|obliques|abs|biceps|triceps|forearm|trapezius|deltoids|upper-back|lower-back|" +
         "adductors|quadriceps|tibialis|calves|hamstring|gluteal").Split('|').ToHashSet();

    [Fact]
    public void Detailed_muscles_use_only_known_fine_slugs()
    {
        var data = Load();
        var offenders = data.Exercises
            .SelectMany(e => e.DetailedPrimaryMuscles.Concat(e.DetailedSecondaryMuscles)
                .Where(m => !KnownFineMuscles.Contains(m))
                .Select(m => $"{e.Slug}: '{m}'"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Seeded detailed muscles must be one of the 16 known fine slugs the map renders. Offenders:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void Detailed_muscles_never_repeat_a_slug_across_primary_and_secondary()
    {
        var data = Load();
        var offenders = data.Exercises
            .Where(e => e.DetailedPrimaryMuscles.Intersect(e.DetailedSecondaryMuscles).Any())
            .Select(e => $"{e.Slug}: {string.Join(",", e.DetailedPrimaryMuscles.Intersect(e.DetailedSecondaryMuscles))}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "A muscle cannot be both primary and secondary on the same exercise. Offenders:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void Seed_library_covers_every_category_and_equipment_code()
    {
        var data = Load();
        var usedCategories = data.Exercises.Select(e => e.Category!).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        var usedEquipment = data.Exercises.Select(e => e.Equipment!).ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        Assert.All(data.ValidCategoryCodes, c =>
            Assert.True(usedCategories.Contains(c), $"No seeded exercise uses category '{c}'."));
        Assert.All(data.ValidEquipmentCodes, eq =>
            Assert.True(usedEquipment.Contains(eq), $"No seeded exercise uses equipment '{eq}'."));
    }

    [Fact]
    public void Factory_maps_a_known_exercise_onto_the_domain_aggregate()
    {
        var data = Load();
        var dto = data.Exercises.Single(e => e.Slug == "barbell-bench-press");

        var exercise = ExerciseSeedFactory.Create(dto);

        Assert.Equal("Barbell Bench Press", exercise.DefaultName);
        Assert.Equal(ExerciseType.Strength, exercise.Type);
        Assert.Equal(MovementType.Compound, exercise.MovementType);
        Assert.Equal(Equipment.Barbell, exercise.Equipment);
        Assert.Equal(ExerciseTrackingType.Strength, exercise.TrackingType);
        // Primary + secondaries persisted with the right roles.
        Assert.Contains(exercise.Muscles, m => m.Muscle == MuscleGroup.Chest && m.IsPrimary);
        Assert.Contains(exercise.Muscles, m => m.Muscle == MuscleGroup.Shoulders && !m.IsPrimary);
        // Non-persisted facets are folded into searchable tags.
        Assert.Contains(exercise.Tags, t => t.Tag == "chest");
        Assert.Contains(exercise.Tags, t => t.Tag == "horizontal-push");
        Assert.Contains(exercise.Tags, t => t.Tag == "push");
        // Instructions and safety notes (-> warnings) are persisted.
        Assert.NotEmpty(exercise.Instructions);
        Assert.NotEmpty(exercise.Warnings);
    }

    [Fact]
    public void Factory_derives_tracking_type_when_not_specified()
    {
        var data = Load();
        // A plain cardio entry with no explicit trackingType should derive to Cardio.
        var cardio = data.Exercises.First(e =>
            string.Equals(e.Type, "Cardio", System.StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(e.TrackingType));

        var exercise = ExerciseSeedFactory.Create(cardio);

        Assert.Equal(ExerciseTrackingType.Cardio, exercise.TrackingType);
    }

    [Fact]
    public void Validator_reports_required_fields_unique_keys_and_bad_enums()
    {
        var data = new ExerciseSeedData(
            new[]
            {
                new ExerciseSeedDto
                {
                    // valid baseline
                    Slug = "ok", Name = "Ok Exercise", Description = "d", Category = "chest",
                    Type = "Strength", PrimaryMuscle = "Chest", Equipment = "Barbell",
                    Difficulty = "Beginner", Mechanics = "Compound",
                    Instructions = { "step one" }
                },
                new ExerciseSeedDto
                {
                    // duplicate slug + duplicate name + bad enum + empty instruction + missing description
                    Slug = "ok", Name = "Ok Exercise", Category = "nope",
                    Type = "Strength", PrimaryMuscle = "Chest", Equipment = "Barbell",
                    Difficulty = "WrongLevel", Mechanics = "Compound",
                    Instructions = { "  " }
                }
            },
            ("Chest|Back|Shoulders|Arms|Legs|Core").Split('|').ToHashSet(System.StringComparer.OrdinalIgnoreCase),
            ("Bodyweight|Barbell|Dumbbell|Machine|ResistanceBand").Split('|').ToHashSet(System.StringComparer.OrdinalIgnoreCase),
            ("chest|back").Split('|').ToHashSet(System.StringComparer.OrdinalIgnoreCase));

        var result = new ExerciseSeedDataValidator().Validate(data);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate slug"));
        Assert.Contains(result.Errors, e => e.Contains("duplicate canonical name"));
        Assert.Contains(result.Errors, e => e.Contains("not a valid DifficultyLevel"));
        Assert.Contains(result.Errors, e => e.Contains("not a known code")); // category "nope"
        Assert.Contains(result.Errors, e => e.Contains("empty step"));
        Assert.Contains(result.Errors, e => e.Contains("'description'"));
    }

    [Fact]
    public void Validator_rejects_duplicate_aliases()
    {
        var muscles = ("Chest|Back|Shoulders|Arms|Legs|Core").Split('|').ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        var equipment = ("Bodyweight|Barbell|Dumbbell|Machine|ResistanceBand").Split('|').ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        var categories = ("chest").Split('|').ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        var data = new ExerciseSeedData(
            new[]
            {
                new ExerciseSeedDto
                {
                    Slug = "x", Name = "X", Description = "d", Category = "chest", Type = "Strength",
                    PrimaryMuscle = "Chest", Equipment = "Barbell", Difficulty = "Beginner",
                    Mechanics = "Compound", Instructions = { "step" },
                    Aliases = { "bench", "bench" } // duplicate within the exercise
                }
            },
            muscles, equipment, categories);

        var result = new ExerciseSeedDataValidator().Validate(data);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate alias"));
    }
}
