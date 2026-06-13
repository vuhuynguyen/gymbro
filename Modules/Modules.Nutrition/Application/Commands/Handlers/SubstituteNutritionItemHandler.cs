using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Queries;
using Modules.NutritionModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class SubstituteNutritionItemHandler(
    IDailyNutritionLogRepository logRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<SubstituteNutritionItemCommand, Result>
{
    public async Task<Result> Handle(SubstituteNutritionItemCommand request, CancellationToken cancellationToken)
    {
        var log = await logRepository.GetOwnByDateAsync(currentUser.UserId, request.Date, cancellationToken);
        if (log == null)
            return Result.Failure(NotFound("NotFound", "No nutrition log for that date."));
        if (!log.IsOpen)
            return Result.Failure(Conflict("DailyLog.Closed", "This day is closed and can no longer be edited."));

        var item = log.FindItem(request.ItemId);
        if (item == null)
            return Result.Failure(NotFound("NotFound", "Logged item not found."));

        var summariesResult = await mediator.Send(new ResolveFoodSummariesQuery([request.FoodId]), cancellationToken);
        if (summariesResult.IsFailure)
            return Result.Failure(summariesResult.Error);
        if (!summariesResult.Value!.TryGetValue(request.FoodId, out var food))
            return Result.Failure(Validation("FoodId", $"Food {request.FoodId} was not found."));

        var quantity = request.Quantity ?? item.Quantity;
        item.Substitute(
            request.FoodId, food.Kind, food.Name, food.ServingLabel, quantity,
            food.EnergyKcal, food.ProteinG, food.CarbsG, food.FatG, food.FiberG, request.Note);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
