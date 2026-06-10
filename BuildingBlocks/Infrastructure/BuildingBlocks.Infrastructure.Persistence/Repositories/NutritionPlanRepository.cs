using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class NutritionPlanRepository(AppDbContext context) : INutritionPlanRepository
{
    public async Task AddAsync(NutritionPlan entity, CancellationToken cancellationToken = default)
        => await context.Set<NutritionPlan>().AddAsync(entity, cancellationToken);

    public async Task<NutritionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Set<NutritionPlan>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<NutritionPlan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Set<NutritionPlan>()
            .Include(p => p.Meals)
            .ThenInclude(m => m.Items)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<NutritionPlan?> GetLatestVersionInTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
        => await context.Set<NutritionPlan>()
            .Where(p => p.TemplateId == templateId)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task ClearPlanStructureAsync(Guid nutritionPlanId, CancellationToken cancellationToken = default)
    {
        // Detach any tracked children so the bulk delete doesn't race the change tracker (mirrors
        // WorkoutPlanRepository.ClearPlanStructureAsync).
        var mealIds = await context.Set<PlanMeal>()
            .AsNoTracking()
            .Where(m => m.NutritionPlanId == nutritionPlanId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        foreach (var entry in context.ChangeTracker.Entries<PlanMealItem>()
                     .Where(e => mealIds.Contains(e.Entity.PlanMealId)).ToList())
            entry.State = EntityState.Detached;
        foreach (var entry in context.ChangeTracker.Entries<PlanMeal>()
                     .Where(e => e.Entity.NutritionPlanId == nutritionPlanId).ToList())
            entry.State = EntityState.Detached;

        if (mealIds.Count > 0)
            await context.Set<PlanMealItem>()
                .Where(i => mealIds.Contains(i.PlanMealId))
                .ExecuteDeleteAsync(cancellationToken);

        await context.Set<PlanMeal>()
            .Where(m => m.NutritionPlanId == nutritionPlanId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public IQueryable<NutritionPlan> Query() => context.Set<NutritionPlan>().AsQueryable();
}
