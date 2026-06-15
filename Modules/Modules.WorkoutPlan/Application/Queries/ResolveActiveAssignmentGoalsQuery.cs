using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.WorkoutPlanModule.Application.Queries;

/// <summary>
/// INTERNAL cross-module lookup (handled in the WorkoutPlan module): the in-gym active-assignment weekly goal
/// (<c>FrequencyDaysPerWeek</c>) for a set of trainees, TENANT-SCOPED — the EF tenant filter is ON, so it sees
/// only assignments in the active gym. Used by the coach roster (the coach's own-gym adherence denominator);
/// when a trainee holds more than one active in-gym assignment, the most-recent by <c>StartDate</c> wins. Carries
/// no caller-facing authorization of its own — it is reached only from the already-gated coach roster handler.
/// Returns a trainee → goal map (trainees with no active in-gym assignment are simply absent).
/// </summary>
public sealed record ResolveActiveAssignmentGoalsQuery(IReadOnlyList<Guid> TraineeIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, int>>>;
