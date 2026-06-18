using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutSessionModule.Application.Commands;

/// <summary>
/// Superset an exercise in a live session with a peer — they share (or start) a group id and rotate
/// together, resting after the round. A null <see cref="PeerExerciseId"/> leaves the superset, so the
/// exercise logs and rests on its own again.
/// </summary>
public sealed record SetExerciseSupersetCommand(
    Guid SessionId,
    Guid ExerciseId,
    Guid? PeerExerciseId) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
