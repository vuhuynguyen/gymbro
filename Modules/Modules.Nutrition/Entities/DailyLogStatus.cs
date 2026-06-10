namespace Modules.NutritionModule.Entities;

/// <summary>
/// Lifecycle of a daily log. A day is Open until it closes (local-midnight rollover), which finalizes Missed
/// items + adherence. Mirrors the session terminal-state guard. Persisted as int.
/// </summary>
public enum DailyLogStatus
{
    Open = 1,
    Closed = 2
}
