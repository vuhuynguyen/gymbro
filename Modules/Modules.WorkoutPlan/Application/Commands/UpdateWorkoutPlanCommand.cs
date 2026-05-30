using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record UpdateWorkoutPlanCommand(
    Guid Id,
    string Name,
    string? Description,
    int? DurationWeeks,
    int? WorkoutsPerWeek) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanUpdate;
}
