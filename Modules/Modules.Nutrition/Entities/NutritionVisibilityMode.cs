namespace Modules.NutritionModule.Entities;

/// <summary>
/// Nutrition-plan assignment visibility (sibling of the WorkoutPlan PlanVisibilityMode). MVP stores the mode
/// + flags for forward-compatibility; read-time redaction is a later phase (the snapshot is always stored
/// full and the trainee currently sees Full). Persisted as int — values are load-bearing.
/// </summary>
public enum NutritionVisibilityMode
{
    Full = 1,
    Guided = 2,
    Blind = 3
}
