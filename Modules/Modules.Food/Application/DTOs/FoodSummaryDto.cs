namespace Modules.FoodModule.Application.DTOs;

/// <summary>
/// The Food module's public, cross-module read shape — the only food data other modules (Nutrition) may
/// consume. Carries the denormalizable facts a plan item / log item snapshots at authoring/log time. Lives in
/// the owning module's Application namespace (never Entities) per the module-boundary rule.
/// </summary>
public sealed record FoodSummaryDto(
    Guid Id,
    string Name,
    string Kind,
    string ServingLabel,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG);
