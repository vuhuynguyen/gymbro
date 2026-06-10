using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Mapping;

namespace Modules.FoodModule.Application.Queries.Handlers;

public sealed class ResolveFoodSummariesHandler(IFoodRepository repository)
    : IRequestHandler<ResolveFoodSummariesQuery, Result<IReadOnlyDictionary<Guid, FoodSummaryDto>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, FoodSummaryDto>>> Handle(
        ResolveFoodSummariesQuery request,
        CancellationToken cancellationToken)
    {
        var ids = request.FoodIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result<IReadOnlyDictionary<Guid, FoodSummaryDto>>.Success(
                new Dictionary<Guid, FoodSummaryDto>());

        var map = await repository.Query()
            .Where(f => ids.Contains(f.Id))
            .Select(FoodMapping.FoodSummaryProjection)
            .ToDictionaryAsync(f => f.Id, f => f, cancellationToken);

        return Result<IReadOnlyDictionary<Guid, FoodSummaryDto>>.Success(map);
    }
}
