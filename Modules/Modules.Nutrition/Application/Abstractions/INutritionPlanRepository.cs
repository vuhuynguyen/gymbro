using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Abstractions;

/// <summary>Repository for nutrition plan templates. Mirrors <c>IWorkoutPlanRepository</c>.</summary>
public interface INutritionPlanRepository
{
    Task AddAsync(NutritionPlan entity, CancellationToken cancellationToken = default);

    Task<NutritionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a plan with its meals and items, tracked, for a structure edit / delete.</summary>
    Task<NutritionPlan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<NutritionPlan?> GetLatestVersionInTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>Bulk-removes a plan's meals + items (no tracked deletes), mirroring ClearPlanStructureAsync.</summary>
    Task ClearPlanStructureAsync(Guid nutritionPlanId, CancellationToken cancellationToken = default);

    IQueryable<NutritionPlan> Query();
}
