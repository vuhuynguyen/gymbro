using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Modules.WorkoutSessionModule.Application.Abstractions;

namespace Modules.WorkoutSessionModule.Infrastructure.Persistence;

/// <summary>Registers the WorkoutSession module's repositories and its model contributor.</summary>
public static class WorkoutSessionModulePersistenceExtensions
{
    public static IServiceCollection AddWorkoutSessionModulePersistence(this IServiceCollection services)
    {
        services.AddSingleton<IModelConfiguration, WorkoutSessionModelConfiguration>();
        services.AddScoped<IWorkoutSessionRepository, WorkoutSessionRepository>();
        services.AddScoped<IPerformedExerciseRepository, PerformedExerciseRepository>();
        services.AddScoped<IPerformedSetRepository, PerformedSetRepository>();
        return services;
    }
}
