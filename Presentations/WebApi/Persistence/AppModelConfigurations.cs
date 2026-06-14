using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Modules.ExerciseModule.Infrastructure.Persistence;
using Modules.FoodModule.Infrastructure.Persistence;
using Modules.NutritionModule.Infrastructure.Persistence;
using Modules.UserModule.Infrastructure.Persistence;
using Modules.WorkoutPlanModule.Infrastructure.Persistence;
using Modules.WorkoutSessionModule.Infrastructure.Persistence;

namespace WebApi.Persistence;

/// <summary>
/// The complete set of model contributors that make up the <c>AppDbContext</c> model: the kernel's own (outbox),
/// each feature module's, and the cross-module FK configs. The runtime resolves the same set from DI (each
/// module's <c>AddXModulePersistence</c> plus the composition root); this list is the design-time / test
/// equivalent for code paths that build the context without the full DI container.
/// </summary>
public static class AppModelConfigurations
{
    public static IReadOnlyList<IModelConfiguration> All { get; } = new IModelConfiguration[]
    {
        new CoreModelConfiguration(),
        new UserModelConfiguration(),
        new ExerciseModelConfiguration(),
        new WorkoutPlanModelConfiguration(),
        new WorkoutSessionModelConfiguration(),
        new FoodModelConfiguration(),
        new NutritionModelConfiguration(),
        new CrossModuleModelConfiguration(),
    };
}
