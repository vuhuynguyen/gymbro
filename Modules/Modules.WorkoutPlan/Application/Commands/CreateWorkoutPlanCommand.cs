using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record CreateWorkoutPlanCommand(
    string Name,
    string? Description,
    int? DurationWeeks,
    int? WorkoutsPerWeek) : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanCreate;
}
