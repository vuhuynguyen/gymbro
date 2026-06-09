using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Identity.DependencyInjection;
using BuildingBlocks.Infrastructure.Persistence.DependencyInjection;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Modules.ExerciseModule;
using Modules.IdentityModule;
using Modules.IdentityModule.Application.Abstractions;
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

static void ConfigureJson(JsonSerializerOptions options)
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
}

builder.Services.AddControllers().AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
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
// Drains the transactional outbox (domain events committed alongside their state change) out-of-band.
builder.Services.AddOutboxProcessing(builder.Configuration);
// Read-only safety net: periodically reports drift between the Identity store (AppUser) and the domain
// store (User), which are linked only by convention (AppUser.DomainUserId == User.Id) with no cross-store FK.
builder.Services.AddCrossStoreReconciliation(builder.Configuration);
// OpenTelemetry traces + metrics (OTLP export opt-in via OpenTelemetry:OtlpEndpoint / OTEL_EXPORTER_OTLP_ENDPOINT).
builder.Services.AddGymBroObservability(builder.Configuration);
builder.Services.AddSingleton<IPermissionService, PermissionService>();
builder.Services.AddScoped<ITenantRoleResolver, TenantRoleResolver>();
builder.Services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();
// Per-request memo so the membership/role lookup runs once per request instead of 2-4 times.
builder.Services.AddScoped<IRequestRoleCache, RequestRoleCache>();

var jwtSecret = builder.Configuration["Jwt:Secret"];
// The length check alone is insufficient: the committed placeholders are >= 32
// chars, so a deploy that forgets to override Jwt:Secret would boot with a PUBLICLY-KNOWN signing key —
// letting anyone forge an `is_admin=true` token, which bypasses every EF tenant filter and admits all
// admin endpoints. Reject the shipped sentinels (and any value carrying the "REPLACE" marker) so the
// placeholder can never reach a running API.
string[] jwtSecretPlaceholders =
[
    "REPLACE_WITH_A_SECURE_SECRET_KEY_MIN_32_CHARS",
    "REPLACE_VIA_USER_SECRETS_OR_ENV_MIN_32_CHARS",
];
var isPlaceholderSecret = !string.IsNullOrWhiteSpace(jwtSecret)
    && (jwtSecret.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase)
        || jwtSecretPlaceholders.Contains(jwtSecret, StringComparer.Ordinal));
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32 || isPlaceholderSecret)
    throw new InvalidOperationException(
        "Jwt:Secret is missing, shorter than 32 characters, or still set to the committed placeholder. " +
        "Configure a strong, unique secret (>= 32 chars) via user-secrets or environment variables " +
        "(e.g. `dotnet user-secrets set \"Jwt:Secret\" \"<random 48+ char value>\"`) before starting the API.");

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
        // the user's current SecurityStamp. Rotating the stamp (logout-all, password change/reset,
        // admin delete) invalidates all live access tokens. The current stamp is cached briefly to
        // avoid a DB hit per request; worst-case latency for a still-cached user is CachePolicies.SecurityStamp.
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

                var stampCache = ctx.HttpContext.RequestServices
                    .GetRequiredService<ISecurityStampCacheService>();
                if (!await stampCache.IsStampValidAsync(
                        subjectId,
                        tokenStamp,
                        ctx.HttpContext.RequestAborted))
                {
                    ctx.Fail("Token has been revoked.");
                }
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
builder.Services.AddGymBroDistributedInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName);

// Health: /health is a dependency-free liveness probe; /health/ready additionally verifies DB
// connectivity (both EF contexts) and Redis when configured via the "ready"-tagged checks.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" })
    .AddCheck<OutboxHealthCheck>("outbox", tags: new[] { "ready" });

builder.Services.AddGymBroModuleCaches();

// Production CORS is opt-in via config (Cors:AllowedOrigins). A same-origin deploy (SPA reverse-proxied to
// /api) needs none; a cross-origin SPA must list its origins. Dev keeps the fixed localhost:4200 policy.
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "GymBroPortal",
            policy =>
            {
                policy
                    // Dev runs several local clients on varying ports — the Angular portal (:4200) and the
                    // Flutter web dev server (an ephemeral port). Allow any loopback origin so they all work
                    // without re-listing ports. SetIsOriginAllowed is required because AllowCredentials (the
                    // refresh-token cookie rides /api/auth calls) cannot be combined with AllowAnyOrigin.
                    .SetIsOriginAllowed(origin =>
                        Uri.TryCreate(origin, UriKind.Absolute, out var o) && o.IsLoopback)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // refresh-token cookie must ride along on /api/auth calls
            });
    });
}
else if (corsAllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "ProductionCors",
            policy =>
            {
                policy
                    .WithOrigins(corsAllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
    });
}

var app = builder.Build();

// Behind a TLS-terminating reverse proxy (nginx / Cloudflare / Caddy) the API receives plain HTTP, so
// Request.IsHttps would be false (→ the `Secure` refresh cookie is dropped) and Request.RemoteIpAddress
// would be the proxy's (→ auth rate limits partition everyone into one bucket). Honour X-Forwarded-Proto/-For
// to recover the real scheme + client IP. Opt-in via ForwardedHeaders:Enabled: turn it on ONLY when the API
// sits behind a trusted proxy (it is never published directly in the compose / managed-runtime deploys),
// otherwise a direct client could spoof these headers. Must run before HTTPS redirect, CORS, the rate
// limiter, auth, and any cookie-writing endpoint. KnownNetworks/KnownProxies are cleared because the proxy's
// container IP isn't fixed — trust is enforced by the proxy being the only reachable path to the API.
//
// ForwardLimit is the number of proxy hops to walk back through X-Forwarded-For. The production stack puts
// TWO proxies in front of the API (Caddy terminates TLS → nginx serves the SPA and proxies /api → api:8080),
// so nginx forwards "X-Forwarded-For: <client>, <caddy>" and we must peel BOTH to reach the real client IP —
// the default of 1 would stop at Caddy's container IP and still collapse every client into one rate-limit
// bucket. Override via ForwardedHeaders:ForwardLimit for other topologies (e.g. Cloudflare → 1).
if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
{
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
        ForwardLimit = app.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 2)
    };
    forwardedOptions.KnownIPNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedOptions);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.Title = "Fitness API Docs";
        options.Theme = ScalarTheme.BluePlanet;
    });
}

// In Development the app is hit directly over plain HTTP (e.g. the Flutter client → http://localhost:5216).
// Forcing an HTTPS redirect bounces those calls to the untrusted dev cert on :7015 and breaks them. Outside
// Development we still redirect (behind Caddy/nginx TLS termination this is a harmless no-op — no HTTPS port
// is bound in the container).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseCors("GymBroPortal");
}
else if (corsAllowedOrigins.Length > 0)
{
    app.UseCors("ProductionCors");
}

app.UseRateLimiter();

// Header/media-type API version negotiation (version is not in the URL). Rejects an explicit unsupported
// version, defaults to latest otherwise, and echoes the resolved X-Api-Version. See WebApi.Http.ApiVersioning.
app.UseMiddleware<ApiVersionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();
app.MapControllers();

// Liveness: app is up (no dependencies checked). Readiness: DB reachable (the "ready"-tagged check).
// Both are anonymous (no [Authorize]) so orchestrators can probe without a token.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Migrate-and-exit mode for a dedicated pre-deploy step (e.g. a one-off container run / job):
// `dotnet WebApi.dll --migrate` applies both EF chains and exits, so serving replicas can boot with
// verify-only (no startup races).
if (args.Contains("--migrate"))
{
    await DatabaseMigrationStartup.ApplyMigrationsAsync(app.Services);
    return;
}

// Seed-and-exit entrypoints for the exercise master data (a one-off CLI run, no serving):
//   dotnet run --project Presentations/WebApi -- --seed-exercises     (non-destructive insert-missing)
//   dotnet run --project Presentations/WebApi -- --reseed-exercises   (destructive full refresh: soft-deletes
//                                                                       obsolete entries, upserts the rest)
// Both verify migrations first (same policy as normal startup) so the tables exist. See
// docs/master-data/EXERCISE_SEEDING.md.
if (args.Contains("--seed-exercises") || args.Contains("--reseed-exercises"))
{
    await DatabaseMigrationStartup.EnsureMigrationsAppliedAsync(app.Services);
    var seedMode = args.Contains("--reseed-exercises") ? ExerciseSeedMode.Reseed : ExerciseSeedMode.InsertMissing;
    await ExerciseMasterDataSeeder.RunAsync(app.Services, seedMode);
    return;
}

await DatabaseMigrationStartup.EnsureMigrationsAppliedAsync(app.Services);
await DbSeeder.SeedAsync(app.Services);

app.Run();
