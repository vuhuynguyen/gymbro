using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record CreatePlanAssignmentCommand(
    Guid TraineeId,
    Guid PlanId,
    DateOnly StartDate,
    int FrequencyDaysPerWeek,
    PlanVisibilityMode VisibilityMode,
    bool HideExercises,
    bool HideSetsReps,
    bool HideFutureWorkouts,
    bool DisableTraineeEditing,
    string? SnapshotJson) : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanAssign;
}
