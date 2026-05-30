using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutSessionModule.Application.Commands;

public enum ExerciseUpdateAction { Skip, Substitute }

public sealed record UpdatePerformedExerciseCommand(
    Guid SessionId,
    Guid ExerciseId,
    ExerciseUpdateAction Action,
    Guid? SubstituteExerciseId,
    string? Notes) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
