using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.FoodModule.Application.Commands.Handlers;

public sealed class DeleteFoodHandler(IFoodRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteFoodCommand, Result>
{
    public async Task<Result> Handle(DeleteFoodCommand request, CancellationToken cancellationToken)
    {
        // Platform-admin path: admin bypasses the EF filter, so GetByIdAsync finds any food.
        var food = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (food == null)
            return Result.Failure(NotFound("NotFound", "Food not found."));

        // Soft-delete (ISoftDelete): AppDbContext turns the Remove → SaveChanges into an UPDATE.
        repository.Remove(food);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
