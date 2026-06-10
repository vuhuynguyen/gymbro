using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Queries;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class AddAdhocNutritionItemHandler(
    IDailyNutritionLogRepository logRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AddAdhocNutritionItemCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddAdhocNutritionItemCommand request, CancellationToken cancellationToken)
    {
        var log = await logRepository.GetOwnByDateAsync(currentUser.UserId, request.Date, cancellationToken);
        if (log == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Open today's nutrition before logging an off-plan item."));
        if (!log.IsOpen)
            return Result<Guid>.Failure(Conflict("DailyLog.Closed", "This day is closed and can no longer be edited."));

        var mealName = string.IsNullOrWhiteSpace(request.MealName) ? "Off-plan" : request.MealName.Trim();
        LoggedItemData data;

        if (request.FoodId is { } foodId && foodId != Guid.Empty)
        {
            // Catalog food: resolve its snapshot + kind.
            var summariesResult = await mediator.Send(new ResolveFoodSummariesQuery([foodId]), cancellationToken);
            if (summariesResult.IsFailure)
                return Result<Guid>.Failure(summariesResult.Error);
            if (!summariesResult.Value!.TryGetValue(foodId, out var food))
                return Result<Guid>.Failure(Validation("FoodId", $"Food {foodId} was not found."));

            data = new LoggedItemData(
                PlanMealItemId: null, MealName: mealName, ScheduledTime: null, Order: 0,
                FoodId: foodId,
                Kind: food.Kind,
                FoodNameSnapshot: food.Name, ServingLabelSnapshot: food.ServingLabel, Quantity: request.Quantity,
                EnergyKcal: food.EnergyKcal, ProteinG: food.ProteinG, CarbsG: food.CarbsG,
                FatG: food.FatG, FiberG: food.FiberG);
        }
        else if (!string.IsNullOrWhiteSpace(request.CustomName))
        {
            // Inline custom food: no catalog entry — the item carries its own snapshot (FoodId null).
            data = new LoggedItemData(
                PlanMealItemId: null, MealName: mealName, ScheduledTime: null, Order: 0,
                FoodId: null,
                Kind: string.IsNullOrWhiteSpace(request.CustomKind) ? "Food" : request.CustomKind.Trim(),
                FoodNameSnapshot: request.CustomName.Trim(),
                ServingLabelSnapshot: string.IsNullOrWhiteSpace(request.ServingLabel) ? "1 serving" : request.ServingLabel.Trim(),
                Quantity: request.Quantity,
                EnergyKcal: request.EnergyKcal, ProteinG: request.ProteinG, CarbsG: request.CarbsG,
                FatG: request.FatG, FiberG: request.FiberG);
        }
        else
        {
            return Result<Guid>.Failure(Validation("FoodId", "Provide a catalog FoodId or a custom food name."));
        }

        var item = log.AddAdhocItem(data, request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(item.Id);
    }
}
