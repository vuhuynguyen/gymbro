using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Infrastructure.Persistence;

/// <summary>Registers the User module's repositories and its model contributor.</summary>
public static class UserModulePersistenceExtensions
{
    public static IServiceCollection AddUserModulePersistence(this IServiceCollection services)
    {
        services.AddSingleton<IModelConfiguration, UserModelConfiguration>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserTenantRoleRepository, UserTenantRoleRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        return services;
    }
}
