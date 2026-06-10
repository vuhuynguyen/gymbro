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
