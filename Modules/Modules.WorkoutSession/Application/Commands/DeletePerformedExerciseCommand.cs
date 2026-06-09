using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutSessionModule.Application.Commands;

/// <summary>
/// Fully removes a performed exercise (and, by FK cascade, its logged sets) from an in-progress
/// session. Unlike <see cref="UpdatePerformedExerciseCommand"/> with <c>Skip</c> — which keeps the
/// row as a skipped record — this deletes it outright.
/// </summary>
public sealed record DeletePerformedExerciseCommand(
    Guid SessionId,
    Guid ExerciseId) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
