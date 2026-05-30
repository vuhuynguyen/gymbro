using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Infrastructure.Identity;

namespace WebApi.Composition;

public static class DbSeeder
{
    private const string DefaultAdminEmail = "admin@gymbro.local";
    private const string DefaultAdminPassword = "Admin@123456";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        await SeedAdminAsync(userManager, environment, configuration, logger);

        // Exercise catalog seeding runs in every environment.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ExerciseCatalogSeeder.SeedGlobalCatalogAsync(db, logger);
    }

    private static async Task SeedAdminAsync(
        UserManager<AppUser> userManager,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger logger)
    {
        // Explicit admin credentials supplied via configuration/env take precedence and are
        // honored in any environment (e.g. first-run provisioning of staging/production).
        var configuredEmail = configuration["Seed:AdminEmail"];
        var configuredPassword = configuration["Seed:AdminPassword"];
        var hasConfiguredCredentials =
            !string.IsNullOrWhiteSpace(configuredEmail) && !string.IsNullOrWhiteSpace(configuredPassword);

        // Otherwise only seed the well-known default admin in Development. In non-Development
        // environments without configured credentials, skip admin seeding entirely so the
        // hardcoded default credential is never created outside dev.
        if (!hasConfiguredCredentials && !environment.IsDevelopment())
        {
            logger.LogInformation(
                "Skipping default admin seeding: host is not Development and no Seed:AdminEmail/Seed:AdminPassword configured.");
            return;
        }

        var adminEmail = hasConfiguredCredentials ? configuredEmail! : DefaultAdminEmail;
        var adminPassword = hasConfiguredCredentials ? configuredPassword! : DefaultAdminPassword;

        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null)
            return;

        var admin = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = adminEmail,
            Email = adminEmail,
            NormalizedUserName = adminEmail.ToUpperInvariant(),
            NormalizedEmail = adminEmail.ToUpperInvariant(),
            EmailConfirmed = true
        };
        admin.SetPlatformAdmin(true);

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to seed admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        logger.LogInformation("Seeded platform admin: {Email}", adminEmail);
    }
}
