using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Commands;

// All are TENANT-SCOPED trainee writes on the caller's own daily log, mirroring workout sessions: they are
// ITenantAuthorizedRequest (RequiredPermission = NutritionLogCreate, held by Owner AND Client) hosted on the
// tenant-scoped api/nutrition/log endpoints, declaratively gated by AuthorizationBehavior +
// TenantResolutionMiddleware (membership). Handlers still scope strictly to currentUser.UserId (defense in
// depth) and operate only on an OPEN day. A nutrition day is unique per (TraineeId, LocalDate) globally, so
// its TenantId is the active gym when the day was first created.

/// <summary>Completion-first: mark a planned item Completed or Skipped.</summary>
public sealed record SetNutritionItemStatusCommand(DateOnly Date, Guid ItemId, LoggedItemStatus Status, string? Note)
    : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionLogCreate;
}

/// <summary>Swap a planned item's food (records provenance; counts as adherent).</summary>
public sealed record SubstituteNutritionItemCommand(DateOnly Date, Guid ItemId, Guid FoodId, decimal? Quantity, string? Note)
    : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionLogCreate;
}

/// <summary>
/// Log an off-plan (ad-hoc) item on the day, already completed. Either a catalog [FoodId] OR an inline
/// custom food ([CustomName] + the snapshot fields) — a trainee can log something not in the catalog
/// without polluting it (the item just carries its own snapshot, no FoodId).
/// </summary>
public sealed record AddAdhocNutritionItemCommand(
    DateOnly Date,
    Guid? FoodId,
    decimal Quantity,
    string? MealName,
    string? Note,
    string? CustomName = null,
    string? CustomKind = null,
    string? ServingLabel = null,
    decimal? EnergyKcal = null,
    decimal? ProteinG = null,
    decimal? CarbsG = null,
    decimal? FatG = null,
    decimal? FiberG = null) : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionLogCreate;
}

/// <summary>Remove an ad-hoc item from the day (planned items can't be removed — skip them).</summary>
public sealed record RemoveNutritionItemCommand(DateOnly Date, Guid ItemId)
    : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionLogCreate;
}
