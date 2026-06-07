using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record UpdatePlanAssignmentToLatestVersionCommand(
    Guid AssignmentId,
    string? SnapshotJson) : IRequest<Result<bool>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanAssign;
}
