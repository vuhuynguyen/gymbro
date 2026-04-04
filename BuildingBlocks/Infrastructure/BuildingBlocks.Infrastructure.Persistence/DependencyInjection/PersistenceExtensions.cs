using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Infrastructure.Persistence.Services;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.ExerciseModule.Application.Abstractions;

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
        
        return services;
    }
}