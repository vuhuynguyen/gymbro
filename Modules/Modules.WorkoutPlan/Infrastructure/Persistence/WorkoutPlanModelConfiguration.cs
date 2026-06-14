using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Modules.WorkoutPlanModule.Infrastructure.Persistence;

/// <summary>Contributes the WorkoutPlan module's entity configurations to the shared model. The
/// PlanWorkoutExercise→Exercise FK config is cross-module and lives at the composition root.</summary>
public sealed class WorkoutPlanModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkoutPlanModelConfiguration).Assembly);
}
