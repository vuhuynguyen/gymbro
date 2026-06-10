using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Queries;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class ReplaceNutritionPlanStructureHandler(
    INutritionPlanRepository repository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<ReplaceNutritionPlanStructureCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReplaceNutritionPlanStructureCommand request, CancellationToken cancellationToken)
    {
        var foodIds = request.Meals
            .SelectMany(m => m.Items)
            .Select(i => i.FoodId)
            .Distinct()
            .ToList();

        // Cross-module validation + snapshot resolution via the Food contract (mirrors how the workout
        // structure handler validates/resolves exercises).
        var validation = await mediator.Send(new ValidateFoodIdsQuery(foodIds), cancellationToken);
        if (validation.IsFailure)
            return Result<Guid>.Failure(validation.Error);

        var summariesResult = await mediator.Send(new ResolveFoodSummariesQuery(foodIds), cancellationToken);
        if (summariesResult.IsFailure)
            return Result<Guid>.Failure(summariesResult.Error);
        var summaries = summariesResult.Value!;

        var mapped = request.Meals
            .Select(m => new PlanMealData(
                m.Name,
                m.Order,
                m.ScheduledTime,
                m.DayApplicability,
                m.Items
                    .Select(i => ToItemData(i, summaries))
                    .ToList()))
            .ToList();

        var current = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (current == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var latest = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;
        if (current.Id != latest.Id)
            return Result<Guid>.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and edit the latest version."));

        var next = NutritionPlan.CreateNewVersion(latest, currentUser.UserId, request.Name, request.Description);
        next.ReplaceStructure(mapped);
        await repository.AddAsync(next, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<Guid>.Failure(Conflict("Conflict", "This plan was modified concurrently. Refresh and try again."));
        }

        return Result<Guid>.Success(next.Id);
    }

    private static PlanMealItemData ToItemData(
        NutritionPlanItemInput input,
        IReadOnlyDictionary<Guid, FoodSummaryDto> summaries)
    {
        summaries.TryGetValue(input.FoodId, out var food);
        return new PlanMealItemData(
            input.FoodId,
            input.Order,
            input.Quantity,
            food?.Name ?? "(food)",
            food?.ServingLabel ?? "1 serving",
            food?.EnergyKcal,
            food?.ProteinG,
            food?.CarbsG,
            food?.FatG,
            food?.FiberG);
    }
}
