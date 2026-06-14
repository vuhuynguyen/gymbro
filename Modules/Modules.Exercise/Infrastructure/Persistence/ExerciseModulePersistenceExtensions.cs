using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Modules.ExerciseModule.Application.Abstractions;

namespace Modules.ExerciseModule.Infrastructure.Persistence;

/// <summary>Registers the Exercise module's repositories and its model contributor.</summary>
public static class ExerciseModulePersistenceExtensions
{
    public static IServiceCollection AddExerciseModulePersistence(this IServiceCollection services)
    {
        services.AddSingleton<IModelConfiguration, ExerciseModelConfiguration>();
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        return services;
    }
}
