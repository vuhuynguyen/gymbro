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
using Microsoft.IdentityModel.Tokens;
using Modules.ExerciseModule;
using Modules.IdentityModule;
using Modules.IdentityModule.DependencyInjection;
using Modules.UserModule;
using Modules.UserModule.Application.Authorization;
using Modules.WorkoutPlanModule;
using Modules.WorkoutSessionModule;
using WebApi.Composition;
using WebApi.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSingleton<IPermissionService, PermissionService>();
builder.Services.AddScoped<ITenantRoleResolver, TenantRoleResolver>();
builder.Services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();

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
                    .AllowAnyMethod();
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

app.UseAuthentication();
app.UseAuthorization();

// Must run after authentication: validates X-Tenant-Id membership before the tenant
// context (and EF global filters) trust it.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

await DatabaseMigrationStartup.EnsureMigrationsAppliedAsync(app.Services);
await DbSeeder.SeedAsync(app.Services);

app.Run();
