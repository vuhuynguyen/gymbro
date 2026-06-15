using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public sealed class ResolveExerciseNamesHandler(IExerciseRepository repository)
    : IRequestHandler<ResolveExerciseNamesQuery, Result<IReadOnlyDictionary<Guid, string>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, string>>> Handle(
        ResolveExerciseNamesQuery request,
        CancellationToken cancellationToken)
    {
        var ids = request.ExerciseIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result<IReadOnlyDictionary<Guid, string>>.Success(new Dictionary<Guid, string>());

        // Project to (Id, Name) and read untracked — ToDictionaryAsync is a client operator, so without an
        // explicit Select EF materializes full tracked Exercise rows just to build the map. (Audit finding 17.)
        var names = await repository.Query()
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.DefaultName })
            .ToDictionaryAsync(e => e.Id, e => e.DefaultName, cancellationToken);

        return Result<IReadOnlyDictionary<Guid, string>>.Success(names);
    }
}
