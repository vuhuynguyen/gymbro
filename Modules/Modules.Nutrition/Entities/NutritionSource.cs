namespace Modules.NutritionModule.Entities;

/// <summary>Whether a day's log was opened from a plan or is purely ad-hoc. Mirrors SessionSource.</summary>
public enum NutritionSource
{
    FromAssignment = 1,
    Adhoc = 2
}
