using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Infrastructure.Persistence;

public sealed class NutritionPlanAssignmentRepository(DbContext context) : INutritionPlanAssignmentRepository
{
    public async Task AddAsync(NutritionPlanAssignment entity, CancellationToken cancellationToken = default)
        => await context.Set<NutritionPlanAssignment>().AddAsync(entity, cancellationToken);

    public async Task<NutritionPlanAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Set<NutritionPlanAssignment>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Remove(NutritionPlanAssignment entity) => context.Set<NutritionPlanAssignment>().Remove(entity);

    public IQueryable<NutritionPlanAssignment> Query() => context.Set<NutritionPlanAssignment>().AsQueryable();

    public IQueryable<NutritionPlanAssignment> QueryOwnAcrossGyms(Guid traineeId)
        => context.Set<NutritionPlanAssignment>()
            .IgnoreQueryFilters()
            .Where(a => a.TraineeId == traineeId && !a.IsDeleted);
}
