using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Outbox;
using BuildingBlocks.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Infrastructure.Persistence.Services;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.FoodModule.Application.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
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
        // IMPORTANT: Use AddDbContext, never AddDbContextPool. AppDbContext.ApplyGlobalFilters uses
        // Expression.Constant(this) to capture a live reference to CurrentUser/TenantContext in the
        // compiled EF query filter. This is safe because both properties delegate to
        // IHttpContextAccessor (AsyncLocal) on every call. Pool-reuse would share one context instance
        // across requests; that is currently harmless, but it creates a latent correctness risk that
        // is best eliminated at the registration layer.
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Database"));
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IDbContextServices, DbContextServices>();

        // Transactional-outbox dispatcher (the hosted polling loop lives at the composition root).
        services.AddScoped<OutboxDispatcher>();

        // Generic repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Specific repositories
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        services.AddScoped<IFoodRepository, FoodRepository>();
        services.AddScoped<INutritionPlanRepository, NutritionPlanRepository>();
        services.AddScoped<INutritionPlanAssignmentRepository, NutritionPlanAssignmentRepository>();
        services.AddScoped<IDailyNutritionLogRepository, DailyNutritionLogRepository>();
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