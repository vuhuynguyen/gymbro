using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.FoodModule.Application.Commands.Handlers;

public sealed class CreateFoodHandler(IFoodRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateFoodCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateFoodCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FoodKind>(request.Food.Kind, ignoreCase: true, out var kind))
            return Result<Guid>.Failure(Validation("Food.Kind", $"Invalid food kind: '{request.Food.Kind}'."));

        // Admin write path: the caller is a platform admin, so the EF filter is already bypassed and this
        // scopes the duplicate check to the global catalog (mirrors CreateExerciseHandler's name check).
        var exists = await repository.Query()
            .AnyAsync(f => f.TenantId == null && f.Name == request.Food.Name, cancellationToken);
        if (exists)
            return Result<Guid>.Failure(Conflict("Conflict", "A food with this name already exists."));

        var food = Food.CreateGlobal(
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
