using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Modules.NutritionModule.Application.Abstractions;

namespace Modules.NutritionModule.Infrastructure.Persistence;

/// <summary>Registers the Nutrition module's repositories and its model contributor.</summary>
public static class NutritionModulePersistenceExtensions
{
    public static IServiceCollection AddNutritionModulePersistence(this IServiceCollection services)
    {
        services.AddSingleton<IModelConfiguration, NutritionModelConfiguration>();
        services.AddScoped<INutritionPlanRepository, NutritionPlanRepository>();
        services.AddScoped<INutritionPlanAssignmentRepository, NutritionPlanAssignmentRepository>();
        services.AddScoped<IDailyNutritionLogRepository, DailyNutritionLogRepository>();
        services.AddScoped<IMetricEntryRepository, MetricEntryRepository>();
        return services;
    }
}
