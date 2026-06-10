using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.FoodModule.Application.Commands;

/// <summary>Shared payload for a food's editable fields (used by create/update).</summary>
public sealed record FoodInput(
    string Name,
    string Kind,
    string ServingLabel,
    decimal? ServingSizeGrams,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG,
    string? Brand);

/// <summary>Creates a GLOBAL catalog food (platform-admin only, mirrors CreateExerciseCommand).</summary>
public sealed record CreateFoodCommand(FoodInput Food) : IRequest<Result<Guid>>, IPlatformAdminRequest;

/// <summary>Creates a TENANT-CUSTOM food (Owner only). Owned by the active gym.</summary>
public sealed record CreateCustomFoodCommand(FoodInput Food)
    : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanCreate;
}

/// <summary>Updates a global catalog food (platform-admin only).</summary>
public sealed record UpdateFoodCommand(Guid Id, FoodInput Food)
    : IRequest<Result>, IPlatformAdminRequest;

/// <summary>Soft-deletes a global catalog food (platform-admin only).</summary>
public sealed record DeleteFoodCommand(Guid Id) : IRequest<Result>, IPlatformAdminRequest;
