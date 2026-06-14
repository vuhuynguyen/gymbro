using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Outbox;
using BuildingBlocks.Infrastructure.Persistence.Services;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Persistence.DependencyInjection;

public static class PersistenceExtensions
{
    /// <summary>
    /// Registers the persistence KERNEL: the single <c>AppDbContext</c>, the unit of work, audit/tenant
    /// services, the outbox dispatcher, the generic repository, and the kernel's own model contributor (the
    /// transactional outbox). Feature repositories and per-module model contributors are registered by each
    /// module's <c>AddXModulePersistence</c>; the composition root adds the cross-module FK contributor.
    /// </summary>
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

        // Module repositories depend on DbContext (not the concrete app context) so they need not reference the
        // kernel's concrete project — map DbContext to the single app context here.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IDbContextServices, DbContextServices>();

        // The kernel's own model contributor (outbox). Module contributors come from each module's
        // AddXModulePersistence; the cross-module FK contributor comes from the composition root.
        services.AddSingleton<IModelConfiguration, CoreModelConfiguration>();

        // Transactional-outbox dispatcher (the hosted polling loop lives at the composition root).
        services.AddScoped<OutboxDispatcher>();

        // Generic repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
