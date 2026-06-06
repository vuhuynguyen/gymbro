using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record UpdatePlanAssignmentCommand(
    Guid AssignmentId,
    DateOnly? StartDate,
    int FrequencyDaysPerWeek,
    PlanVisibilityMode VisibilityMode,
    bool HideExercises,
    bool HideSetsReps,
    bool HideFutureWorkouts,
    bool DisableTraineeEditing) : IRequest<Result<bool>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanAssign;
}
