using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.FoodModule.Application.Commands.Handlers;

public sealed class CreateCustomFoodHandler(
    IFoodRepository repository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : IRequestHandler<CreateCustomFoodCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomFoodCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        if (!Enum.TryParse<FoodKind>(request.Food.Kind, ignoreCase: true, out var kind))
            return Result<Guid>.Failure(Validation("Food.Kind", $"Invalid food kind: '{request.Food.Kind}'."));

        var food = Food.CreateForTenant(
            tenantId,
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

        await repository.AddAsync(food, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(food.Id);
    }
}
