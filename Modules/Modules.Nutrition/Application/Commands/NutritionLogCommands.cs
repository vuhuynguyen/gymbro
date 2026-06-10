using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Commands;

// All are SELF-SCOPED writes on the caller's own daily log (no tenant context). Classified
// ImperativeGuarded in TenantAuthorizationExemptions; handlers scope strictly to currentUser.UserId and
// operate only on an OPEN day.

/// <summary>Completion-first: mark a planned item Completed or Skipped.</summary>
public sealed record SetNutritionItemStatusCommand(DateOnly Date, Guid ItemId, LoggedItemStatus Status, string? Note)
    : IRequest<Result>;

/// <summary>Swap a planned item's food (records provenance; counts as adherent).</summary>
public sealed record SubstituteNutritionItemCommand(DateOnly Date, Guid ItemId, Guid FoodId, decimal? Quantity, string? Note)
    : IRequest<Result>;

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
    decimal? FiberG = null) : IRequest<Result<Guid>>;

/// <summary>Remove an ad-hoc item from the day (planned items can't be removed — skip them).</summary>
public sealed record RemoveNutritionItemCommand(DateOnly Date, Guid ItemId) : IRequest<Result>;
