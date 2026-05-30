using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Infrastructure.Persistence.Services;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.UserModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Abstractions;

namespace BuildingBlocks.Infrastructure.Persistence.DependencyInjection;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Database"));
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        
        services.AddScoped<IDbContextServices, DbContextServices>();
        services.AddScoped<ITranslationService, TranslationService>();
        
        // Generic repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Specific repositories
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserTenantRoleRepository, UserTenantRoleRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IWorkoutPlanRepository, WorkoutPlanRepository>();
        services.AddScoped<IPlanAssignmentRepository, PlanAssignmentRepository>();
        services.AddScoped<IWorkoutSessionRepository, WorkoutSessionRepository>();
        services.AddScoped<IPerformedExerciseRepository, PerformedExerciseRepository>();
        services.AddScoped<IPerformedSetRepository, PerformedSetRepository>();

        return services;
    }
}