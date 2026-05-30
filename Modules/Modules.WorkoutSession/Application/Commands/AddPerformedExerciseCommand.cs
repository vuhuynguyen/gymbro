using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Commands;

public sealed record AddPerformedExerciseCommand(
    Guid SessionId,
    Guid ExerciseId,
    Guid? PlanWorkoutExerciseId,
    int Order,
    string? Notes) : IRequest<Result<PerformedExerciseDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
