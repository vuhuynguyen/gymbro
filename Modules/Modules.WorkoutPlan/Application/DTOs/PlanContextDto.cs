namespace Modules.WorkoutPlanModule.Application.DTOs;

/// <summary>
/// Lightweight plan context for a plan assignment, used to enrich workout-session views with the
/// owning program name and to derive the plan week a session falls in (sequence-based plans have no
/// stored week/phase — the week is computed from <see cref="StartDate"/> and the session date).
/// </summary>
public sealed record PlanContextDto(
    string ProgramName,
    DateOnly StartDate,
    int FrequencyDaysPerWeek,
    int? DurationWeeks);
