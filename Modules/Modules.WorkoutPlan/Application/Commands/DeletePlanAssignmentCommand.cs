using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record DeletePlanAssignmentCommand(Guid AssignmentId) : IRequest<Result<bool>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanAssign;
}
