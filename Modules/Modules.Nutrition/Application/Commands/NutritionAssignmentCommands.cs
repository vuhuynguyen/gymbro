using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Commands;

/// <summary>Assigns a nutrition plan to a trainee, pinning the version + a point-in-time snapshot.</summary>
public sealed record CreateNutritionAssignmentCommand(
    Guid TraineeId,
    Guid PlanId,
    DateOnly StartDate,
    DateOnly? EndDate,
    NutritionVisibilityMode VisibilityMode,
    bool HideMacroTargets,
    bool DisableTraineeEditing) : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanAssign;
}

/// <summary>Edits a nutrition-plan assignment's configuration in place (keeps the pinned version + snapshot).
/// Mirrors UpdatePlanAssignmentCommand, adapted to nutrition fields.</summary>
public sealed record UpdateNutritionAssignmentCommand(
    Guid AssignmentId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    NutritionVisibilityMode VisibilityMode,
    bool HideMacroTargets,
    bool DisableTraineeEditing) : IRequest<Result<bool>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanAssign;
}

/// <summary>Revokes (soft-deletes) a nutrition-plan assignment. Mirrors DeletePlanAssignmentCommand.</summary>
public sealed record DeleteNutritionAssignmentCommand(Guid AssignmentId)
    : IRequest<Result<bool>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanAssign;
}

/// <summary>Pause (deactivate) or resume (reactivate) a nutrition-plan assignment.
/// Mirrors SetPlanAssignmentActiveCommand.</summary>
public sealed record SetNutritionAssignmentActiveCommand(Guid AssignmentId, bool Active)
    : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanAssign;
}
