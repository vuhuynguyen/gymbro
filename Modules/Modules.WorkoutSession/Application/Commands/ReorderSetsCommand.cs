using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.WorkoutSessionModule.Application.Commands;

/// <summary>
/// Reorders the logged sets of an in-progress session's exercise. <paramref name="SetIds"/> is the full set
/// of the exercise's set ids in their new order; the handler renumbers <c>SetNumber</c> to match.
/// </summary>
public sealed record ReorderSetsCommand(
    Guid SessionId,
    Guid ExerciseId,
    IReadOnlyList<Guid> SetIds) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
