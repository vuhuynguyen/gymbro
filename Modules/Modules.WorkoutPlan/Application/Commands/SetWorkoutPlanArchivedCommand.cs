using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.WorkoutPlanModule.Application.Commands;

/// <summary>Archive (retire) or unarchive a workout plan template.</summary>
public sealed record SetWorkoutPlanArchivedCommand(Guid Id, bool Archived)
    : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanUpdate;
}
