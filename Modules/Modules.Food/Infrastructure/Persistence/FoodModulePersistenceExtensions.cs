using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Modules.FoodModule.Application.Abstractions;

namespace Modules.FoodModule.Infrastructure.Persistence;

/// <summary>Registers the Food module's repositories and its model contributor.</summary>
public static class FoodModulePersistenceExtensions
{
    public static IServiceCollection AddFoodModulePersistence(this IServiceCollection services)
    {
        services.AddSingleton<IModelConfiguration, FoodModelConfiguration>();
        services.AddScoped<IFoodRepository, FoodRepository>();
        return services;
    }
}
