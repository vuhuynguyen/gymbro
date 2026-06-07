using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.IdentityModule.Infrastructure.Identity;

namespace WebApi.Composition;

public static class DatabaseMigrationStartup
{
    /// <summary>
    /// Default behaviour is <b>verify-only</b>: if either EF chain has pending migrations the app fails fast,
    /// so a half-migrated database never serves traffic <i>and</i> serving replicas never race each other to
    /// migrate. This is the safe default for multi-instance deploys (apply migrations from a dedicated
    /// pre-deploy step first — see below).
    ///
    /// <para>
    /// Set <c>Database:AutoMigrate=true</c> to apply pending migrations at startup instead. Use this for
    /// local/dev and single-instance deploys. For a <b>multi-instance</b> rollout, run migrations from a
    /// dedicated pre-deploy job (e.g. a one-off container run with <c>Database__AutoMigrate=true</c>, the
    /// <c>--migrate</c> entrypoint, or <c>dotnet ef database update</c>) and keep AutoMigrate <b>off</b> on
    /// the serving replicas.
    /// </para>
    /// </summary>
    public static async Task EnsureMigrationsAppliedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        if (configuration.GetValue("Database:AutoMigrate", false))
        {
            logger.LogInformation(
                "Database:AutoMigrate is enabled — applying pending migrations for both EF contexts.");
            await appDb.Database.MigrateAsync();
            await identityDb.Database.MigrateAsync();
            return;
        }

        var pendingApp = (await appDb.Database.GetPendingMigrationsAsync()).ToList();
        var pendingIdentity = (await identityDb.Database.GetPendingMigrationsAsync()).ToList();

        if (pendingApp.Count == 0 && pendingIdentity.Count == 0)
            return;

        var message =
            "Database is not fully migrated. Apply both migration chains before starting the API (or set " +
            "Database:AutoMigrate=true for single-instance/dev). Run: " +
            "`dotnet ef database update --context AppDbContext` and " +
            "`dotnet ef database update --context IdentityDbContext`. " +
            $"Pending AppDbContext: [{string.Join(", ", pendingApp)}]; " +
            $"Pending IdentityDbContext: [{string.Join(", ", pendingIdentity)}].";

        logger.LogCritical(message);
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Unconditionally applies both EF migration chains. Used by the <c>--migrate</c> entrypoint for a
    /// dedicated pre-deploy migration step (a one-off container run / job), independent of <c>Database:AutoMigrate</c>.
    /// </summary>
    public static async Task ApplyMigrationsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Applying both EF migration chains (--migrate).");
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        logger.LogInformation("Migrations applied.");
    }
}
