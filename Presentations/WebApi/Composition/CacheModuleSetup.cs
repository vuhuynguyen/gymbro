using Modules.ExerciseModule.Application.Caching;
using Modules.FoodModule.Application.Caching;

namespace WebApi.Composition;

public static class CacheModuleSetup
{
    public static IServiceCollection AddGymBroModuleCaches(this IServiceCollection services)
    {
        services.AddScoped<ExerciseCatalogSearchReader>();
        services.AddScoped<ExerciseCatalogDetailReader>();
        services.AddScoped<ExerciseCatalogCache>();

        services.AddScoped<FoodCatalogSearchReader>();
        services.AddScoped<FoodCatalogDetailReader>();
        services.AddScoped<FoodCatalogCache>();

        return services;
    }
}
