using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Modules.WorkoutPlanModule.Application.Abstractions;

namespace Modules.WorkoutPlanModule.Infrastructure.Persistence;

/// <summary>Registers the WorkoutPlan module's repositories and its model contributor.</summary>
public static class WorkoutPlanModulePersistenceExtensions
{
    public static IServiceCollection AddWorkoutPlanModulePersistence(this IServiceCollection services)
    {
        services.AddSingleton<IModelConfiguration, WorkoutPlanModelConfiguration>();
        services.AddScoped<IWorkoutPlanRepository, WorkoutPlanRepository>();
        services.AddScoped<IPlanAssignmentRepository, PlanAssignmentRepository>();
        return services;
    }
}
