using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record DeleteWorkoutPlanCommand(Guid Id) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanDelete;
}
