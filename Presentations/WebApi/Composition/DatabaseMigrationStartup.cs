using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.IdentityModule.Infrastructure.Identity;

namespace WebApi.Composition;

public static class DatabaseMigrationStartup
{
    public static async Task EnsureMigrationsAppliedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var pendingApp = (await appDb.Database.GetPendingMigrationsAsync()).ToList();
        var pendingIdentity = (await identityDb.Database.GetPendingMigrationsAsync()).ToList();

        if (pendingApp.Count == 0 && pendingIdentity.Count == 0)
            return;

        var message =
            "Database is not fully migrated. Run both migration chains: " +
            $"`dotnet ef database update --context AppDbContext` and " +
            $"`dotnet ef database update --context IdentityDbContext`. " +
            $"Pending AppDbContext: [{string.Join(", ", pendingApp)}]; " +
            $"Pending IdentityDbContext: [{string.Join(", ", pendingIdentity)}].";

        logger.LogCritical(message);
        throw new InvalidOperationException(message);
    }
}
