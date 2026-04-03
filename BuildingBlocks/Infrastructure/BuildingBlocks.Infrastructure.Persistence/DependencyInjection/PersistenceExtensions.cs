using BuildingBlocks.Application.Interfaces;
using BuildingBlocks.Infrastructure.Persistence.Services;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        
        services.AddScoped<IDbContextServices, DbContextServices>();
        services.AddScoped<ITranslationService, TranslationService>();
        
        return services;
    }
}