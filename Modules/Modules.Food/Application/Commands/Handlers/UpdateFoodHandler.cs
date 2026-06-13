using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Application.Caching;
using Modules.FoodModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.FoodModule.Application.Commands.Handlers;

public sealed class UpdateFoodHandler(
    IFoodRepository repository,
    IUnitOfWork unitOfWork,
    FoodCatalogCache catalogCache)
    : IRequestHandler<UpdateFoodCommand, Result>
{
    public async Task<Result> Handle(UpdateFoodCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FoodKind>(request.Food.Kind, ignoreCase: true, out var kind))
            return Result.Failure(Validation("Food.Kind", $"Invalid food kind: '{request.Food.Kind}'."));

        // Platform-admin path: admin bypasses the EF filter, so GetByIdAsync finds any food.
        var food = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (food == null)
            return Result.Failure(NotFound("NotFound", "Food not found."));

        food.UpdateDetails(
            request.Food.Name,
            kind,
            request.Food.ServingLabel,
            request.Food.ServingSizeGrams,
            request.Food.EnergyKcal,
            request.Food.ProteinG,
            request.Food.CarbsG,
            request.Food.FatG,
            request.Food.FiberG,
            request.Food.Brand);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The edited food must vanish from cached detail + search rows.
        await catalogCache.InvalidateDetailAsync(request.Id, cancellationToken);
        await catalogCache.InvalidateSearchAsync(cancellationToken);

        return Result.Success();
    }
}
