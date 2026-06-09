namespace BuildingBlocks.Shared.Tracking;

/// <summary>
/// How an exercise is logged. Drives which metrics a set requires/shows across the API, the plan builder,
/// and the session loggers. Lives in the shared kernel (not a feature module's <c>Entities</c>) because it is a
/// cross-cutting concept consumed by the Exercise (owner), WorkoutPlan (prescription) and WorkoutSession (logging)
/// modules; it crosses module/HTTP boundaries as a string (like <c>ExerciseType</c>). Defaults to <see cref="Strength"/>.
/// </summary>
public enum ExerciseTrackingType
{
    Strength = 1,
    Bodyweight = 2,
    Cardio = 3,
    Timed = 4,
    Hiit = 5,
    Mobility = 6,
    Custom = 7
}
