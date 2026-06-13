using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Commands;

public sealed record NutritionPlanItemInput(Guid FoodId, int Order, decimal Quantity);

public sealed record NutritionPlanMealInput(
    string Name,
    int Order,
    TimeOnly? ScheduledTime,
    DayApplicability DayApplicability,
    IReadOnlyList<NutritionPlanItemInput> Items);

public sealed record CreateNutritionPlanCommand(string Name, string? Description)
    : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanCreate;
}

/// <summary>
/// Replaces a plan's structure — metadata + meals travel together so a builder save lands as ONE new
/// version (mirrors ReplaceWorkoutPlanStructureCommand). Returns the new version id.
/// </summary>
public sealed record ReplaceNutritionPlanStructureCommand(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<NutritionPlanMealInput> Meals) : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanUpdate;
}

/// <summary>
/// Publishes the plan's draft head, turning it into the immutable version trainees and assignments see. This is
/// the only action that advances the published version — plain edits keep replacing the draft in place. Returns
/// the newly published version number.
/// </summary>
public sealed record PublishNutritionPlanCommand(Guid Id) : IRequest<Result<int>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanUpdate;
}

public sealed record DeleteNutritionPlanCommand(Guid Id) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanDelete;
}

/// <summary>Archive (retire) or unarchive a nutrition plan template. Mirrors SetWorkoutPlanArchivedCommand.</summary>
public sealed record SetNutritionPlanArchivedCommand(Guid Id, bool Archived)
    : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanUpdate;
}
