namespace BuildingBlocks.Shared.Nutrition;

/// <summary>
/// When a planned meal applies, evaluated by <see cref="NutritionScheduleRules"/>. Lives in the shared kernel
/// (like <c>ExerciseTrackingType</c>) so the server rule and both client mirrors agree on one definition.
/// Persisted as int — <b>do not renumber</b>.
/// </summary>
public enum DayApplicability
{
    EveryDay = 1,
    TrainingDay = 2,
    RestDay = 3
}
