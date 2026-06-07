using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutPlanModule.Application.Commands;

// Metadata-only edit. Like a structure edit it forks a new version; it returns that new version's id so the
// caller can re-point to the latest version for its next edit (otherwise the next edit 409s on a stale id).
public sealed record UpdateWorkoutPlanCommand(
    Guid Id,
    string Name,
    string? Description,
    int? DurationWeeks,
    int? WorkoutsPerWeek) : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanUpdate;
}
