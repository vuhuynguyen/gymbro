using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Abstractions;

public interface IPlanAssignmentRepository
{
    Task AddAsync(PlanAssignment entity, CancellationToken cancellationToken = default);
    Task<PlanAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Remove(PlanAssignment entity);
    IQueryable<PlanAssignment> Query();
}
