using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
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
    ICurrentUser currentUser,
    IMemoryCache cache)
    : IRequestHandler<DeleteExerciseCommand, Result>
{
    public async Task<Result> Handle(
        DeleteExerciseCommand request,
        CancellationToken cancellationToken)
    {
        if (AdminPolicy.Deny(currentUser) is { } denied) return denied;

        var exercise = await repository.GetByIdAsync(request.ExerciseId, cancellationToken);

        if (exercise == null)
        {
            return Result.Failure(Error.NotFound("Exercise not found."));
        }

        var id = exercise.Id;
        repository.Remove(exercise);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        cache.Remove(ExerciseCatalogCacheKeys.DetailScoped(id, "admin"));

        return Result.Success();
    }
}
