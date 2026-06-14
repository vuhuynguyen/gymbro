namespace BuildingBlocks.Shared.Nutrition;

/// <summary>
/// The single source of truth for nutrition recurrence: given a day's training/rest type, decide which planned
/// meals apply. Pure and deterministic so it can be unit-tested exhaustively and mirrored verbatim by the Flutter
/// (<c>nutrition_schedule.dart</c>) and Angular (<c>nutrition-schedule.ts</c>) clients — exactly as
/// <c>ExerciseTrackingRules</c> is mirrored today. The server applies it at daily-log snapshot time (which planned
/// meals to seed for the date); the clients apply it to preview a day's meals and, later, to drive local reminders.
/// </summary>
public static class NutritionScheduleRules
{
    /// <summary>
    /// Whether a meal with the given <paramref name="applicability"/> applies on a day of the given type.
    /// <c>EveryDay</c> always applies; <c>TrainingDay</c>/<c>RestDay</c> gate on <paramref name="isTrainingDay"/>.
    /// An unknown value is treated as <c>EveryDay</c> (fail-open: never silently drop a planned meal).
    /// </summary>
    public static bool Applies(DayApplicability applicability, bool isTrainingDay) => applicability switch
    {
        DayApplicability.TrainingDay => isTrainingDay,
        DayApplicability.RestDay => !isTrainingDay,
        _ => true
    };
}
