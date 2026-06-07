using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Infrastructure.Identity.DependencyInjection;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.DependencyInjection;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.ExerciseModule;
using Modules.ExerciseModule.Application.Caching;
using Modules.IdentityModule;
using Modules.IdentityModule.DependencyInjection;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.UserModule;
using Modules.UserModule.Application.Authorization;
using Modules.UserModule.Entities;
using Modules.WorkoutPlanModule;
using Modules.WorkoutSessionModule;
using Modules.WorkoutSessionModule.Entities;
using Testcontainers.PostgreSql;
using WebApi.Composition;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Spins up a throwaway Postgres, applies the AppDbContext migration chain, wires the real MediatR
/// pipeline + persistence + authorization services around a mutable <see cref="TestPrincipal"/>, and
/// seeds a two-trainee tenant (plus a second tenant) so tests can attempt cross-trainee and
/// cross-tenant reads through the genuine handlers.
///
/// Requires a Docker daemon. When none is reachable, <see cref="SkipReason"/> is set and the
/// integration facts skip themselves (via <c>Skip.If</c>) instead of failing the suite.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private ServiceProvider? _services;

    public TestPrincipal Principal { get; } = new();

    /// <summary>Opt-in failure switches for the cross-store transaction tests (no-ops when off).</summary>
    public FailureToggle Toggle { get; } = new();

    /// <summary>Non-null when no database could be reached; tells the facts to skip.</summary>
    public string? SkipReason { get; private set; }

    /// <summary>True on CI runners (GitHub Actions sets CI=true), where skipping integration tests is forbidden.</summary>
    private static bool IsContinuousIntegration =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    // --- Seeded identifiers (one tenant with two clients + an owner, plus a second tenant) ---
    public Guid TenantId { get; private set; }
    public Guid OwnerId { get; private set; }       // Owner role in TenantId -> WorkoutLogViewAll
    public Guid ClientAId { get; private set; }      // Client role -> WorkoutLogViewOwn only
    public Guid ClientBId { get; private set; }      // Client role -> WorkoutLogViewOwn only
    public Guid SessionAId { get; private set; }     // owned by ClientA
    public Guid SessionBId { get; private set; }     // owned by ClientB

    public Guid OtherTenantId { get; private set; }
    public Guid OtherOwnerId { get; private set; }   // Owner in OtherTenantId

    public async Task InitializeAsync()
    {
        // Prefer an externally-provided database (CI service container, or a local Postgres) so the suite
        // can run without Docker; otherwise fall back to a throwaway Testcontainers Postgres.
        string connectionString;
        var external = Environment.GetEnvironmentVariable("GYMBRO_TEST_DB");
        if (!string.IsNullOrWhiteSpace(external))
        {
            connectionString = external;
        }
        else
        {
            try
            {
                // Build() validates the Docker endpoint, so it must run inside the guard alongside StartAsync.
                _container = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .Build();
                await _container.StartAsync();
                connectionString = _container.GetConnectionString();
            }
            catch (Exception ex)
            {
                // In CI we must NEVER silently skip integration coverage — that would report green with zero
                // cross-tenant/isolation tests actually run. Fail loudly so a misconfigured pipeline (no
                // Postgres service container AND no Docker) is caught. Locally, fall back to a skip.
                if (IsContinuousIntegration)
                    throw new InvalidOperationException(
                        "Integration tests require a database in CI: set GYMBRO_TEST_DB to a reachable Postgres " +
                        "(e.g. a service container) or provide a Docker daemon. Refusing to skip in CI.", ex);

                SkipReason = $"No GYMBRO_TEST_DB set and Docker is not available: {ex.Message}";
                return;
            }
        }

        _services = BuildServices(connectionString);

        // Apply both migration chains against the fresh DB (mirrors the two `dotnet ef` chains).
        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        }

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    /// <summary>Dispatch a request through the full MediatR pipeline in a fresh scope.</summary>
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }

    /// <summary>Run work in a fresh DI scope (like one request) — used by tests that resolve handlers
    /// directly to bypass the pipeline, or read state through the contexts.</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public async Task InScopeAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }

    private IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("Service provider unavailable (container never started).");

    private ServiceProvider BuildServices(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                // TokenService / RefreshTokenService read these; values only need to be valid, not secret.
                ["Jwt:Secret"] = "integration-test-signing-secret-which-is-long-enough-1234567890",
                ["Jwt:Issuer"] = "gymbro-tests",
                ["Jwt:Audience"] = "gymbro-tests",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddGymBroDistributedInfrastructure(configuration, "Test");
        services.AddGymBroModuleCaches();
        services.AddDataProtection();   // UserManager's default token providers need IDataProtectionProvider.

        // Same MediatR composition as Program.cs (all five module assemblies + pipeline behaviors).
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(ExerciseModuleAssembly.Assembly);
            cfg.RegisterServicesFromAssembly(IdentityModuleAssembly.Assembly);
            cfg.RegisterServicesFromAssembly(UserModuleAssembly.Assembly);
            cfg.RegisterServicesFromAssembly(WorkoutPlanModuleAssembly.Assembly);
            cfg.RegisterServicesFromAssembly(WorkoutSessionModuleAssembly.Assembly);
        });

        services.AddValidatorsFromAssembly(ExerciseModuleAssembly.Assembly);
        services.AddValidatorsFromAssembly(IdentityModuleAssembly.Assembly);
        services.AddValidatorsFromAssembly(UserModuleAssembly.Assembly);
        services.AddValidatorsFromAssembly(WorkoutPlanModuleAssembly.Assembly);
        services.AddValidatorsFromAssembly(WorkoutSessionModuleAssembly.Assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PlatformAdminBehavior<,>));

        services.AddPersistence(configuration);
        services.AddIdentity(configuration);          // HTTP-bound CurrentUser — overridden below
        services.AddIdentityModule(configuration);    // UserManager, TokenService, RefreshTokenService, ICrossStoreTransaction
        services.AddSingleton<IPermissionService, PermissionService>();
        // Per-request role memo — TenantRoleResolver depends on it (registered in Program.cs the same way).
        services.AddScoped<IRequestRoleCache, RequestRoleCache>();
        services.AddScoped<ITenantRoleResolver, TenantRoleResolver>();
        services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();

        // The mutable principal stands in for the HTTP-scoped CurrentUser/TenantContext. Registered LAST
        // so it overrides the HTTP-bound CurrentUser that AddIdentity wires up.
        services.AddSingleton(Principal);
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<TestPrincipal>());
        services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<TestPrincipal>());

        // Opt-in failure injection for the cross-store transaction tests; no-ops (off) for everyone else.
        services.AddSingleton(Toggle);
        services.AddScoped<INotificationHandler<UserRegisteredNotification>, ThrowingRegisteredHandler>();
        services.AddScoped<INotificationHandler<UserDeletedNotification>, ThrowingDeletedHandler>();

        return services.BuildServiceProvider();
    }

    private async Task SeedAsync()
    {
        TenantId = Guid.NewGuid();
        OwnerId = Guid.NewGuid();
        ClientAId = Guid.NewGuid();
        ClientBId = Guid.NewGuid();
        OtherTenantId = Guid.NewGuid();
        OtherOwnerId = Guid.NewGuid();

        // Inserts bypass the global query filters; impersonate an admin so nothing is filtered on write.
        Principal.Become(OwnerId, TenantId, isAdmin: true);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Set<Tenant>().AddRange(
            Tenant.Create("Iron Gym", OwnerId),
            Tenant.Create("Rival Gym", OtherOwnerId));

        db.Set<User>().AddRange(
            User.Create(OwnerId, "Olivia Owner"),
            User.Create(ClientAId, "Aaron Client"),
            User.Create(ClientBId, "Bianca Client"),
            User.Create(OtherOwnerId, "Oscar Other"));

        db.Set<UserTenantRole>().AddRange(
            UserTenantRole.Create(OwnerId, TenantId, TenantRole.Owner),
            UserTenantRole.Create(ClientAId, TenantId, TenantRole.Client),
            UserTenantRole.Create(ClientBId, TenantId, TenantRole.Client),
            UserTenantRole.Create(OtherOwnerId, OtherTenantId, TenantRole.Owner));

        var sessionA = WorkoutSession.Start(
            ClientAId, TenantId, SessionSource.Adhoc, null, null, "Push Day", null, "UTC", null);
        var sessionB = WorkoutSession.Start(
            ClientBId, TenantId, SessionSource.Adhoc, null, null, "Pull Day", null, "UTC", null);
        db.Set<WorkoutSession>().AddRange(sessionA, sessionB);

        await db.SaveChangesAsync();

        SessionAId = sessionA.Id;
        SessionBId = sessionB.Id;
    }
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres-integration";
}

/// <summary>Per-run switches that make the injected handlers below throw, simulating a second-store
/// failure mid cross-store transaction. Off by default, so they are no-ops for every other test.</summary>
public sealed class FailureToggle
{
    public bool FailProvisioning;
    public bool FailIdentityCleanup;

    public void Reset()
    {
        FailProvisioning = false;
        FailIdentityCleanup = false;
    }
}

internal sealed class ThrowingRegisteredHandler(FailureToggle toggle)
    : INotificationHandler<UserRegisteredNotification>
{
    public Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
    {
        if (toggle.FailProvisioning)
            throw new InvalidOperationException("forced provisioning failure");
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingDeletedHandler(FailureToggle toggle)
    : INotificationHandler<UserDeletedNotification>
{
    public Task Handle(UserDeletedNotification notification, CancellationToken cancellationToken)
    {
        if (toggle.FailIdentityCleanup)
            throw new InvalidOperationException("forced identity cleanup failure");
        return Task.CompletedTask;
    }
}
