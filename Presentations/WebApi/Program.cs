using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Identity.DependencyInjection;
using BuildingBlocks.Infrastructure.Persistence.DependencyInjection;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Modules.ExerciseModule.Application.Caching;
using Microsoft.IdentityModel.Tokens;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;
using Modules.ExerciseModule;
using Modules.IdentityModule;
using Modules.IdentityModule.DependencyInjection;
using Modules.UserModule;
using Modules.UserModule.Application.Authorization;
using Modules.WorkoutPlanModule;
using Modules.WorkoutSessionModule;
using WebApi.Composition;
using WebApi.HealthChecks;
using WebApi.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Structured (JSON) logging outside Development so logs are machine-parseable for aggregation; scopes
// carry the per-request TraceId/RequestId/RequestPath that GlobalExceptionHandler also stamps onto error
// responses, letting an error's traceId be matched to its logs. Development keeps the readable console.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
    });
}

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
});
builder.Services.AddOpenApi();
builder.Services.AddIdentity(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(ExerciseModuleAssembly.Assembly);
    cfg.RegisterServicesFromAssembly(IdentityModuleAssembly.Assembly);
    cfg.RegisterServicesFromAssembly(UserModuleAssembly.Assembly);
    cfg.RegisterServicesFromAssembly(WorkoutPlanModuleAssembly.Assembly);
    cfg.RegisterServicesFromAssembly(WorkoutSessionModuleAssembly.Assembly);
});

builder.Services.AddValidatorsFromAssembly(ExerciseModuleAssembly.Assembly);
builder.Services.AddValidatorsFromAssembly(IdentityModuleAssembly.Assembly);
builder.Services.AddValidatorsFromAssembly(UserModuleAssembly.Assembly);
builder.Services.AddValidatorsFromAssembly(WorkoutPlanModuleAssembly.Assembly);
builder.Services.AddValidatorsFromAssembly(WorkoutSessionModuleAssembly.Assembly);

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PlatformAdminBehavior<,>));

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSingleton<IPermissionService, PermissionService>();
builder.Services.AddScoped<ITenantRoleResolver, TenantRoleResolver>();
builder.Services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();
// Per-request memo so the membership/role lookup runs once per request instead of 2-4 times.
builder.Services.AddScoped<IRequestRoleCache, RequestRoleCache>();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Secret is missing or shorter than 32 characters. Configure a strong secret " +
        "(>= 32 chars) via user-secrets or environment variables before starting the API.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };

        // Tier-2 revocation: every authenticated request re-checks the token's `stamp` claim against
        // the user's current SecurityStamp. Rotating the stamp (logout-all, password change/reset)
        // invalidates all live access tokens. The current stamp is cached briefly to avoid a DB hit
        // per request, so worst-case latency for a still-cached user is SecurityStampCache.Duration.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal;
                var subjectId = principal?.FindFirst("sub")?.Value
                                ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tokenStamp = principal?.FindFirst("stamp")?.Value;

                if (subjectId is null || tokenStamp is null)
                {
                    ctx.Fail("Token missing identity claims.");
                    return;
                }

                var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                var currentStamp = await cache.GetOrCreateAsync(
                    SecurityStampCache.KeyFor(subjectId),
                    async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = SecurityStampCache.Duration;
                        var userManager = ctx.HttpContext.RequestServices
                            .GetRequiredService<UserManager<AppUser>>();
                        var user = await userManager.FindByIdAsync(subjectId);
                        return user is null ? null : await userManager.GetSecurityStampAsync(user);
                    });

                if (currentStamp is null || !string.Equals(currentStamp, tokenStamp, StringComparison.Ordinal))
                    ctx.Fail("Token has been revoked.");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.PlatformAdmin,
        policy => policy.RequireClaim("is_admin", "true"));
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();

// Health: /health is a dependency-free liveness probe; /health/ready additionally verifies DB
// connectivity (both EF contexts) via the "ready"-tagged check.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

// One shared change-token instance evicts all cached exercise-search pages on any catalog mutation.
builder.Services.AddSingleton<ExerciseSearchCacheSignal>();
// The detail cache needs the same cross-scope eviction: its per-tenant + admin entries can't be
// enumerated for targeted removal, so they link to this signal instead.
builder.Services.AddSingleton<ExerciseDetailCacheSignal>();

// Rate limiting for the unauthenticated/abuse-prone auth surface, partitioned per client IP.
// "auth" guards brute-force-able endpoints (login, register, password reset); "auth-refresh" is
// looser because a healthy SPA refreshes its 15-minute access token on a schedule.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("auth-refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "GymBroPortal",
            policy =>
            {
                policy
                    .WithOrigins("http://localhost:4200", "https://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // refresh-token cookie must ride along on /api/auth calls
            });
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.Title = "Fitness API Docs";
        options.Theme = ScalarTheme.BluePlanet;
    });
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseCors("GymBroPortal");
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();
app.MapControllers();

// Liveness: app is up (no dependencies checked). Readiness: DB reachable (the "ready"-tagged check).
// Both are anonymous (no [Authorize]) so orchestrators can probe without a token.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await DatabaseMigrationStartup.EnsureMigrationsAppliedAsync(app.Services);
await DbSeeder.SeedAsync(app.Services);

app.Run();
