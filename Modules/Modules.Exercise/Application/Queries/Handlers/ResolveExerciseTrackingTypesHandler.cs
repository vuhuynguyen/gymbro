using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public sealed class ResolveExerciseTrackingTypesHandler(IExerciseRepository repository)
    : IRequestHandler<ResolveExerciseTrackingTypesQuery, Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>> Handle(
        ResolveExerciseTrackingTypesQuery request,
        CancellationToken cancellationToken)
    {
        var ids = request.ExerciseIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>.Success(
                new Dictionary<Guid, ExerciseTrackingType>());

        var map = await repository.Query()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.TrackingType, cancellationToken);

        return Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>.Success(map);
    }
}
