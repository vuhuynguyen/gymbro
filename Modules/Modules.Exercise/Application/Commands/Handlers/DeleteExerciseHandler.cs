using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.Commands;

namespace Modules.ExerciseModule.Application.Commands.Handlers;

public class DeleteExerciseHandler(
    IExerciseRepository repository,
    IUnitOfWork unitOfWork,
    ExerciseDetailCacheSignal detailCacheSignal,
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

        repository.Remove(exercise);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // A removed exercise must disappear from every cached detail entry — admin AND each per-tenant
        // scope, not just "admin". The scoped keys can't be enumerated, so trip the shared change-token.
        detailCacheSignal.Invalidate();
        // ...and from every cached search page.
        searchCacheSignal.Invalidate();

        return Result.Success();
    }
}
