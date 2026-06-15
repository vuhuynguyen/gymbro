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

        // Project to (Id, TrackingType) and read untracked — avoids materializing full tracked Exercise
        // rows just to build the map (ToDictionaryAsync is a client operator). (Audit finding 17.)
        var map = await repository.Query()
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.TrackingType })
            .ToDictionaryAsync(e => e.Id, e => e.TrackingType, cancellationToken);

        return Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>.Success(map);
    }
}
