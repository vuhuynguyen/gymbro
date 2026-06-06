using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.Commands;

namespace Modules.ExerciseModule.Application.Commands.Handlers;

public class DeleteExerciseHandler(
    IExerciseRepository repository,
    IUnitOfWork unitOfWork,
    IMemoryCache cache,
    ExerciseSearchCacheSignal searchCacheSignal)
    : IRequestHandler<DeleteExerciseCommand, Result>
{
    public async Task<Result> Handle(
        DeleteExerciseCommand request,
        CancellationToken cancellationToken)
    {
        // The Exercise itself is ISoftDelete (SaveChanges converts its delete to a soft-delete), but its
        // children are not — leaving them unloaded would orphan them (the parent's soft-delete is an
        // UPDATE, so the DB-level ON DELETE CASCADE never fires).
        var exercise = await repository.GetForUpdateAsync(request.ExerciseId, cancellationToken);

        if (exercise == null)
        {
            return Result.Failure(Error.NotFound("Exercise not found."));
        }

        var id = exercise.Id;
        repository.Remove(exercise);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        cache.Remove(ExerciseCatalogCacheKeys.DetailScoped(id, "admin"));
        // A removed exercise must disappear from every cached search page.
        searchCacheSignal.Invalidate();

        return Result.Success();
    }
}
