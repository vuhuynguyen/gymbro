using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Commands.Handlers;

public class UpdateExerciseHandler(
    IExerciseRepository repository,
    IUnitOfWork unitOfWork,
    ExerciseCatalogCache catalogCache)
    : IRequestHandler<UpdateExerciseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        UpdateExerciseCommand request,
        CancellationToken cancellationToken)
    {
        var exercise = await repository.GetForUpdateAsync(request.ExerciseId, cancellationToken);

        if (exercise == null)
        {
            return Result<Guid>.Failure(Error.NotFound("Exercise not found."));
        }

        var nameTaken = await repository.Query()
            .AnyAsync(
                x => x.Id != request.ExerciseId && x.DefaultName == request.Name && !x.IsDeleted,
                cancellationToken);

        if (nameTaken)
        {
            return Result<Guid>.Failure(Error.Conflict("Exercise already exists"));
        }

        if (!Enum.TryParse<ExerciseType>(request.Type, ignoreCase: true, out var type))
            return Result<Guid>.Failure(Error.Validation($"Invalid exercise type: '{request.Type}'."));
        if (!Enum.TryParse<MovementType>(request.MovementType, ignoreCase: true, out var movementType))
            return Result<Guid>.Failure(Error.Validation($"Invalid movement type: '{request.MovementType}'."));
        if (!Enum.TryParse<DifficultyLevel>(request.Difficulty, ignoreCase: true, out var difficulty))
            return Result<Guid>.Failure(Error.Validation($"Invalid difficulty: '{request.Difficulty}'."));
        if (!Enum.TryParse<Equipment>(request.Equipment, ignoreCase: true, out var equipment))
            return Result<Guid>.Failure(Error.Validation($"Invalid equipment: '{request.Equipment}'."));

        var muscles = new List<(MuscleGroup, bool)>();
        foreach (var m in request.Muscles)
        {
            if (!Enum.TryParse<MuscleGroup>(m.Muscle, ignoreCase: true, out var muscle))
                return Result<Guid>.Failure(Error.Validation($"Invalid muscle group: '{m.Muscle}'."));
            muscles.Add((muscle, m.IsPrimary));
        }

        exercise.UpdateCatalog(
            request.Name,
            request.Description,
            request.ImageUrl ?? string.Empty,
            type,
            movementType,
            difficulty,
            equipment,
            request.EstimatedCaloriesBurn,
            request.AverageDurationSeconds);

        exercise.ReplaceMuscles(muscles);

        exercise.ReplaceInstructions(request.Instructions ?? Array.Empty<string>());
        exercise.ReplaceTags(request.Tags ?? Array.Empty<string>());
        exercise.ReplaceMedia(
            (request.Media ?? Array.Empty<ExerciseMediaInput>())
                .Select(m => (m.Url, m.Type))
                .ToList());
        exercise.ReplaceWarnings(request.Warnings ?? Array.Empty<string>());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Detail is keyed by exercise id, so evict exactly this entry (the global catalog has no per-tenant
        // cache key). Best-effort — a cache fault won't fail this already-committed write.
        await catalogCache.InvalidateDetailAsync(request.ExerciseId, cancellationToken);
        // Name/attributes/filters may have changed — every cached search page is now suspect.
        await catalogCache.InvalidateSearchAsync(cancellationToken);

        return Result<Guid>.Success(exercise.Id);
    }
}
