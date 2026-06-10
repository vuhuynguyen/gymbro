using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.FoodModule.Application.Queries.Handlers;

public sealed class ValidateFoodIdsHandler(IFoodRepository repository)
    : IRequestHandler<ValidateFoodIdsQuery, Result>
{
    public async Task<Result> Handle(ValidateFoodIdsQuery request, CancellationToken cancellationToken)
    {
        var ids = request.FoodIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result.Success();

        var existingIds = await repository.Query()
            .Where(f => ids.Contains(f.Id))
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        var missing = ids.Except(existingIds).FirstOrDefault();
        if (missing != Guid.Empty)
            return Result.Failure(Validation("FoodId", $"Food {missing} was not found."));

        return Result.Success();
    }
}
