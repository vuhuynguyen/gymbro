namespace Modules.ExerciseModule.Entities;

public enum MuscleGroup
{
    Chest,
    Back,
    Legs,
    Shoulders,
    Arms,
    Core
}

public enum ExerciseType
{
    Strength,
    Cardio,
    Mobility,
    Stretching
}

public enum MovementType
{
    Compound,
    Isolation
}

public enum Equipment
{
    Bodyweight,
    Dumbbell,
    Barbell,
    Machine,
    ResistanceBand,
    // Appended (value 5) so existing persisted int values are unaffected — no migration needed. Cable/pulley
    // movements were previously folded into Machine; split out so the catalog distinguishes cable variants.
    Cable
}

public enum DifficultyLevel
{
    Beginner,
    Intermediate,
    Advanced
}