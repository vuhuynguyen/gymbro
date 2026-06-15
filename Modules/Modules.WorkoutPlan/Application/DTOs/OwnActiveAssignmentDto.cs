namespace Modules.WorkoutPlanModule.Application.DTOs;

/// <summary>
/// A single active (non-deleted) plan assignment belonging to the caller, projected for the self-scoped
/// adherence-goal lookup. Carries just the fields the goal resolution needs: the owning gym
/// (<see cref="TenantId"/>, to tally completed sessions per gym), the prescribed weekly
/// <see cref="FrequencyDaysPerWeek"/>, and the <see cref="StartDate"/> used as the D1 tie-break.
/// </summary>
public sealed record OwnActiveAssignmentDto(
    Guid AssignmentId,
    Guid TenantId,
    int FrequencyDaysPerWeek,
    DateOnly StartDate);
