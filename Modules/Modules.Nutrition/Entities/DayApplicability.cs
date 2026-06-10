namespace Modules.NutritionModule.Entities;

/// <summary>
/// When a planned meal applies. MVP evaluates only <see cref="EveryDay"/>; the training/rest split is stored
/// for forward-compatibility (training-day detection is a later, analytics phase). Persisted as int.
/// </summary>
public enum DayApplicability
{
    EveryDay = 1,
    TrainingDay = 2,
    RestDay = 3
}
