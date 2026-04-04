using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Identity.DependencyInjection;
using BuildingBlocks.Infrastructure.Persistence.DependencyInjection;
using FluentValidation;
using MediatR;
using Modules.ExerciseModule;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddIdentity(builder.Configuration);
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(ExerciseModuleAssembly.Assembly);
});


builder.Services.AddValidatorsFromAssembly(ExerciseModuleAssembly.Assembly);

builder.Services.AddTransient(
    typeof(IPipelineBehavior<,>), 
    typeof(ValidationBehavior<,>)
);

builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Scalar UI
    app.MapScalarApiReference(options =>
    {
        options.Title = "Fitness API Docs";
        options.Theme = ScalarTheme.BluePlanet;
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

