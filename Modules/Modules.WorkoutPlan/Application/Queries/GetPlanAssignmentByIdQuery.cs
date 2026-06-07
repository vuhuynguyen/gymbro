using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

public sealed record GetPlanAssignmentByIdQuery(Guid Id)
    : IRequest<Result<PlanAssignmentForSessionDto>>;

public sealed record PlanAssignmentForSessionDto(
    Guid Id,
    Guid TraineeId,
    PlanVisibilityMode VisibilityMode,
    bool HideExercises,
    bool HideSetsReps,
    bool HideFutureWorkouts,
    bool DisableTraineeEditing);
