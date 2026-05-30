using BuildingBlocks.Infrastructure.Identity.Models;
using BuildingBlocks.Shared.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Identity.DependencyInjection;

public static class IdentityExtensions
{
    public static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<CurrentUser>());

        return services;
    }
}
