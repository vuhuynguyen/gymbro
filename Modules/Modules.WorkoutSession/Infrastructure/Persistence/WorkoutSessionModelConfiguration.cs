using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Modules.WorkoutSessionModule.Infrastructure.Persistence;

/// <summary>Contributes the WorkoutSession module's entity configurations to the shared model. The
/// PerformedExercise→Exercise FK config is cross-module and lives at the composition root.</summary>
public sealed class WorkoutSessionModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkoutSessionModelConfiguration).Assembly);
}
