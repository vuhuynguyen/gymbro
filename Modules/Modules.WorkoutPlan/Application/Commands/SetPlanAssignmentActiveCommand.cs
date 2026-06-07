using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.WorkoutPlanModule.Application.Commands;

/// <summary>Pause (deactivate) or resume (reactivate) a plan assignment.</summary>
public sealed record SetPlanAssignmentActiveCommand(Guid AssignmentId, bool Active)
    : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanAssign;
}
