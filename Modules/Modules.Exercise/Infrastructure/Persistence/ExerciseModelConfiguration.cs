using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Modules.ExerciseModule.Infrastructure.Persistence;

/// <summary>Contributes the Exercise module's entity configurations to the shared model.</summary>
public sealed class ExerciseModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExerciseModelConfiguration).Assembly);
}
