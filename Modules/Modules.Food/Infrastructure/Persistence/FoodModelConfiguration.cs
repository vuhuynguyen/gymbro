using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Modules.FoodModule.Infrastructure.Persistence;

/// <summary>Contributes the Food module's entity configurations to the shared model.</summary>
public sealed class FoodModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FoodModelConfiguration).Assembly);
}
