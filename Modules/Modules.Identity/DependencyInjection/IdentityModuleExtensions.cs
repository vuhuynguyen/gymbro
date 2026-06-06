using BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.IdentityModule.Application.Abstractions;
using Modules.IdentityModule.Infrastructure.Email;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Persistence;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.DependencyInjection;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<TokenService>();
        services.AddScoped<RefreshTokenService>();

        // Periodic purge of expired refresh tokens so the table doesn't grow unbounded.
        services.AddHostedService<RefreshTokenCleanupService>();

        // Atomicity across the Identity store and the domain store (same physical DB, separate contexts).
        services.AddScoped<ICrossStoreTransaction, CrossStoreTransaction>();

        // Email: bind options, then pick the provider. A configured SMTP host wires the real sender;
        // otherwise fall back to the dev logger so local runs need no mail server.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>();
        if (emailOptions?.IsSmtpConfigured == true)
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
