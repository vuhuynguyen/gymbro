using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

// Resolves the LATEST version in the template that <see cref="Id"/> belongs to, then returns that version's
// detail. The plan builder loads through this so opening a stale (non-latest) version id self-heals to the
// editable latest version instead of dead-ending on a 409 at save time (edits must target the latest version).
public sealed record GetLatestWorkoutPlanByIdQuery(Guid Id) : IRequest<Result<WorkoutPlanDetailDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanView;
}
