using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using WebApi.Persistence.Configurations;

namespace WebApi.Persistence;

/// <summary>
/// Contributes the cross-module FK configurations — the four configs that reference TWO modules' entities and so
/// cannot live in either module without it referencing the other (forbidden by the module-boundary rule). The
/// composition root is the one place that legitimately sees every module, so these are wired here.
/// </summary>
public sealed class CrossModuleModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PerformedExerciseConfiguration()); // WorkoutSession → Exercise
        modelBuilder.ApplyConfiguration(new PlanWorkoutExerciseConfiguration()); // WorkoutPlan → Exercise
        modelBuilder.ApplyConfiguration(new LoggedItemConfiguration()); // Nutrition → Food
        modelBuilder.ApplyConfiguration(new PlanMealItemConfiguration()); // Nutrition → Food
    }
}
