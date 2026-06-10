namespace Modules.NutritionModule.Entities;

/// <summary>
/// Per-item planned-vs-actual state. <c>Skipped</c> is an intentional user choice; <c>Missed</c> is the
/// system marking a still-<c>Planned</c> item when the day closes (a no-show) — the two are distinct so a
/// coach can tell "deviated on purpose" from "ghosted". Persisted as int.
/// </summary>
public enum LoggedItemStatus
{
    Planned = 1,
    Completed = 2,
    Skipped = 3,
    Substituted = 4,
    Missed = 5
}
