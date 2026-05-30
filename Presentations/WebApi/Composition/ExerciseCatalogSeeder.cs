using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Entities;

namespace WebApi.Composition;

/// <summary>
/// Seeds real-world Strength / Conditioning defaults into the shared (global) catalog.
/// Uses <see cref="Exercise.CreateGlobal"/>; idempotent by exercise name (<c>TenantId == null</c>).
/// </summary>
internal static class ExerciseCatalogSeeder
{
    public static async Task SeedGlobalCatalogAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var added = 0;
        foreach (var def in Definitions)
        {
            var exists = await db.Exercises
                .IgnoreQueryFilters()
                .AnyAsync(
                    x => x.TenantId == null && !x.IsDeleted && x.DefaultName == def.Name,
                    cancellationToken);

            if (exists)
                continue;

            var exercise = Exercise.CreateGlobal(
                def.Name,
                string.Empty,
                def.Description,
                def.Type,
                def.Movement,
                def.Difficulty,
                def.Equipment,
                def.EstimatedCalories,
                def.AverageDurationSeconds,
                def.Muscles);

            exercise.ReplaceInstructions(def.Instructions);
            exercise.ReplaceTags(def.Tags);

            db.Exercises.Add(exercise);
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} global catalog exercises.", added);
        }
    }

    private readonly record struct CatalogExercise(
        string Name,
        string Description,
        ExerciseType Type,
        MovementType Movement,
        DifficultyLevel Difficulty,
        Equipment Equipment,
        int? EstimatedCalories,
        int? AverageDurationSeconds,
        IReadOnlyList<(MuscleGroup muscle, bool isPrimary)> Muscles,
        IReadOnlyList<string> Instructions,
        IReadOnlyList<string> Tags);

    private static readonly IReadOnlyList<CatalogExercise> Definitions =
    [
        new(
            "Barbell Bench Press",
            "Horizontal press on a flat bench. Builds chest, shoulders, and triceps.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Barbell,
            90,
            240,
            [(MuscleGroup.Chest, true), (MuscleGroup.Shoulders, false), (MuscleGroup.Arms, false)],
            [
                "Retract shoulder blades, feet planted, slight arch if comfortable.",
                "Lower bar to mid-chest with control, then press up in a slight arc."
            ],
            ["push", "upper", "compound", "chest"]),

        new(
            "Incline Dumbbell Press",
            "Press on a 30–45° bench to emphasize upper chest.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Dumbbell,
            85,
            240,
            [(MuscleGroup.Chest, true), (MuscleGroup.Shoulders, false)],
            [
                "Set bench incline; dumbbells stacked over wrists at the bottom.",
                "Press until arms are extended without locking out aggressively."
            ],
            ["push", "upper", "chest", "compound"]),

        new(
            "Pull-Up",
            "Bodyweight vertical pull targeting lats and biceps.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Advanced,
            Equipment.Bodyweight,
            65,
            180,
            [(MuscleGroup.Back, true), (MuscleGroup.Arms, false)],
            [
                "Hang with full elbow extension; engage lats before pulling.",
                "Pull chest toward bar; lower with control."
            ],
            ["pull", "upper", "back", "compound"]),

        new(
            "Barbell Bent-Over Row",
            "Hinge at hips and pull a barbell to the lower torso for mid-back thickness.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Barbell,
            80,
            240,
            [(MuscleGroup.Back, true), (MuscleGroup.Arms, false), (MuscleGroup.Core, false)],
            [
                "Hinge until torso is roughly 45°; brace core.",
                "Row bar toward belly button using back, not just arms."
            ],
            ["pull", "upper", "back", "hinge", "compound"]),

        new(
            "Conventional Deadlift",
            "Hinge pattern lifting a barbell from the floor.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Advanced,
            Equipment.Barbell,
            140,
            300,
            [(MuscleGroup.Legs, true), (MuscleGroup.Back, false)],
            [
                "Bar over mid-foot; hinge to grip outside knees.",
                "Drive floor away, keep bar close; stand tall without hyperextending lumbar."
            ],
            ["lower", "hinge", "compound", "posterior-chain"]),

        new(
            "Back Squat (High-Bar)",
            "Barbell squat with bar on traps; foundational leg strength.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Barbell,
            130,
            300,
            [(MuscleGroup.Legs, true), (MuscleGroup.Core, false)],
            [
                "Brace core; break at hips and knees together.",
                "Depth to parallel or below if mobility allows; drive up evenly."
            ],
            ["lower", "squat", "compound", "legs"]),

        new(
            "Romanian Deadlift",
            "Hip-hinge with soft knee bend; bias hamstrings and glutes.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Barbell,
            100,
            240,
            [(MuscleGroup.Legs, true), (MuscleGroup.Back, false)],
            [
                "Slide bar down legs by pushing hips back.",
                "Feel stretch in hamstrings; return by driving hips forward."
            ],
            ["lower", "hinge", "hamstrings", "compound"]),

        new(
            "Leg Press",
            "Machine squat pattern with fixed path; high load potential for quads.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Beginner,
            Equipment.Machine,
            110,
            300,
            [(MuscleGroup.Legs, true)],
            [
                "Feet mid-platform, shoulder-width; unlock sled with control.",
                "Lower until comfortable depth without rounding low back off pad."
            ],
            ["lower", "machine", "legs", "compound"]),

        new(
            "Walking Lunge",
            "Alternating forward lunge for single-leg stability and quad/glute work.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Beginner,
            Equipment.Bodyweight,
            70,
            300,
            [(MuscleGroup.Legs, true), (MuscleGroup.Core, false)],
            [
                "Tall torso; step long enough so front knee tracks over ankle.",
                "Touch back knee softly; push through front heel to stand."
            ],
            ["lower", "unilateral", "legs"]),

        new(
            "Standing Overhead Press",
            "Strict press barbell from shoulders to overhead.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Barbell,
            95,
            270,
            [(MuscleGroup.Shoulders, true), (MuscleGroup.Arms, false), (MuscleGroup.Core, false)],
            [
                "Grip slightly outside shoulders; ribcage stacked over pelvis.",
                "Clear face with elbows; lock out vertically over mid-foot."
            ],
            ["push", "vertical", "shoulders", "compound"]),

        new(
            "Lat Pulldown",
            "Cable/machine vertical pull scalable for lat development.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Beginner,
            Equipment.Machine,
            75,
            210,
            [(MuscleGroup.Back, true), (MuscleGroup.Arms, false)],
            [
                "Sit tall; slight lean back acceptable with control.",
                "Pull elbows down and slightly back toward ribs."
            ],
            ["pull", "upper", "back", "machine"]),

        new(
            "Seated Cable Row",
            "Horizontal cable pull for rhomboids, lats, and rear delts.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Beginner,
            Equipment.Machine,
            70,
            210,
            [(MuscleGroup.Back, true), (MuscleGroup.Arms, false)],
            ["Neutral spine; initiate by retracting shoulders.", "Row handle to torso without excessive swing."],
            ["pull", "upper", "back", "machine"]),

        new(
            "Dumbbell Lateral Raise",
            "Isolation for medial deltoids.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Dumbbell,
            35,
            180,
            [(MuscleGroup.Shoulders, true)],
            ["Slight bend in elbows fixed through set.", "Raise to shoulder height; control descent."],
            ["shoulders", "isolation", "accessory"]),

        new(
            "Barbell Curl",
            "Classic biceps isolation with a barbell.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Barbell,
            45,
            180,
            [(MuscleGroup.Arms, true)],
            ["Elbows stay under shoulders.", "Flex biceps fully; avoid excessive hip sway."],
            ["arms", "biceps", "isolation"]),

        new(
            "Triceps Rope Pushdown",
            "Cable extension emphasizing triceps.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Machine,
            40,
            180,
            [(MuscleGroup.Arms, true)],
            ["Elbows pinned to ribs.", "Extend fully without shrugging shoulders."],
            ["arms", "triceps", "cable"]),

        new(
            "Pec Deck / Machine Fly",
            "Machine isolation for chest adduction.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Machine,
            55,
            180,
            [(MuscleGroup.Chest, true)],
            ["Slight elbow bend stays constant.", "Bring handles together slowly; squeeze at midline."],
            ["chest", "isolation", "machine"]),

        new(
            "Face Pull (Rope)",
            "External rotation + horizontal pull for rear delts and rotator cuff health.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Machine,
            40,
            180,
            [(MuscleGroup.Shoulders, true), (MuscleGroup.Back, false)],
            ["Elbows high; pull rope toward forehead/nose splitting the rope.", "Pause with upper arms horizontal."],
            ["shoulders", "rear-delt", "health", "cable"]),

        new(
            "Plank",
            "Anti-extension core brace in a prone straight line.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Bodyweight,
            35,
            90,
            [(MuscleGroup.Core, true)],
            ["Forearms or hands under shoulders.", "Brace abs; hips level — no sag or pike."],
            ["core", "anti-extension", "bodyweight"]),

        new(
            "Hanging Knee Raise",
            "Hanging flexion for lower abdominals and hip flexors.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Intermediate,
            Equipment.Bodyweight,
            40,
            120,
            [(MuscleGroup.Core, true), (MuscleGroup.Arms, false)],
            ["Dead hang with minimal swing.", "Flex hips to bring knees toward chest."],
            ["core", "abs", "hanging"]),

        new(
            "Assault Bike / Air Bike",
            "Full-body conditioning on an air-resistance bike.",
            ExerciseType.Cardio,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Machine,
            200,
            600,
            [(MuscleGroup.Legs, true), (MuscleGroup.Arms, false), (MuscleGroup.Core, false)],
            ["Drive arms and legs together for smooth rhythm.", "Adjust pace for intervals or steady state."],
            ["cardio", "conditioning", "intervals"]),

        new(
            "Treadmill Run (Easy)",
            "Steady-state running for aerobic base.",
            ExerciseType.Cardio,
            MovementType.Compound,
            DifficultyLevel.Beginner,
            Equipment.Machine,
            250,
            1200,
            [(MuscleGroup.Legs, true), (MuscleGroup.Core, false)],
            ["Warm up; land mid-foot with short overstride.", "Keep posture tall; breathe rhythmically."],
            ["cardio", "running", "aerobic"]),

        new(
            "Thoracic Extension on Foam Roller",
            "Mobility drill for upper-back extension over a roller.",
            ExerciseType.Mobility,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Bodyweight,
            25,
            120,
            [(MuscleGroup.Back, true), (MuscleGroup.Core, false)],
            ["Roller at mid-thoracic; hands support head.", "Extend over roller in small segments without forcing low back."],
            ["mobility", "t-spine", "warm-up"]),

        new(
            "Pigeon Stretch (Static)",
            "Hip external rotation stretch for glutes and hip capsule.",
            ExerciseType.Stretching,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.Bodyweight,
            20,
            120,
            [(MuscleGroup.Legs, true), (MuscleGroup.Core, false)],
            ["Shin angle ~90° in front if hip allows.", "Square hips; breathe into the stretch for 30–60s per side."],
            ["stretching", "hips", "recovery"]),

        new(
            "Band Pull-Apart",
            "Upper-back activation with a resistance band.",
            ExerciseType.Strength,
            MovementType.Isolation,
            DifficultyLevel.Beginner,
            Equipment.ResistanceBand,
            30,
            120,
            [(MuscleGroup.Back, true), (MuscleGroup.Shoulders, false)],
            ["Arms straight; pull band apart to chest height.", "Squeeze shoulder blades; slow return."],
            ["warm-up", "band", "upper-back"]),

        new(
            "Goblet Squat",
            "Front-loaded squat with one dumbbell; great for patterning depth.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Beginner,
            Equipment.Dumbbell,
            90,
            240,
            [(MuscleGroup.Legs, true), (MuscleGroup.Core, false)],
            ["Hold dumbbell at chest; elbows inside knees at bottom.", "Sit between hips; keep heels down."],
            ["lower", "squat", "dumbbell", "patterning"]),

        new(
            "Farmer's Carry",
            "Loaded carries for grip, core, and posture.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Dumbbell,
            100,
            180,
            [(MuscleGroup.Arms, true), (MuscleGroup.Core, false)],
            [
                "Heavy dumbbells at sides; shoulders packed down.",
                "Walk with short controlled steps; breathe behind brace."
            ],
            ["carry", "grip", "core", "conditioning"])
    ];
}
