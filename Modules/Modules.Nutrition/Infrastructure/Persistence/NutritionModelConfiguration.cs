using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Modules.NutritionModule.Infrastructure.Persistence;

/// <summary>Contributes the Nutrition module's entity configurations to the shared model. The LoggedItem→Food
/// and PlanMealItem→Food FK configs are cross-module and live at the composition root.</summary>
public sealed class NutritionModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NutritionModelConfiguration).Assembly);
}
