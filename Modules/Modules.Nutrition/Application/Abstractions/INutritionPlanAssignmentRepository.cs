using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Abstractions;

/// <summary>Repository for nutrition-plan assignments. Mirrors <c>IPlanAssignmentRepository</c>.</summary>
public interface INutritionPlanAssignmentRepository
{
    Task AddAsync(NutritionPlanAssignment entity, CancellationToken cancellationToken = default);
    Task<NutritionPlanAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Remove(NutritionPlanAssignment entity);
    IQueryable<NutritionPlanAssignment> Query();

    /// <summary>
    /// The caller's own assignments across every gym (the sanctioned tenant-filter bypass, scoped strictly
    /// to <paramref name="traineeId"/>, soft-delete re-applied). Used by the self-scoped daily-log flow to
    /// find the assignment governing a date. Mirrors <c>IWorkoutSessionRepository.QueryOwnAcrossGyms</c>.
    /// </summary>
    IQueryable<NutritionPlanAssignment> QueryOwnAcrossGyms(Guid traineeId);
}
