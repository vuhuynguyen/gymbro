using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Commands.Handlers;

public class CreateExerciseHandler(
    IExerciseRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CreateExerciseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateExerciseCommand request,
        CancellationToken cancellationToken)
    {
        if (AdminPolicy.Deny<Guid>(currentUser) is { } denied) return denied;
        var exists = await repository.Query()
            .AnyAsync(x => x.DefaultName == request.Name, cancellationToken);

        if (exists)
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

        var exercise = Exercise.CreateGlobal(
            request.Name,
            request.ImageUrl ?? string.Empty,
            request.Description,
            type,
            movementType,
            difficulty,
            equipment,
            request.EstimatedCaloriesBurn,
            request.AverageDurationSeconds,
            muscles);

        exercise.ReplaceInstructions(request.Instructions ?? Array.Empty<string>());
        exercise.ReplaceTags(request.Tags ?? Array.Empty<string>());
        exercise.ReplaceMedia(
            (request.Media ?? Array.Empty<ExerciseMediaInput>())
                .Select(m => (m.Url, m.Type))
                .ToList());
        exercise.ReplaceWarnings(request.Warnings ?? Array.Empty<string>());

        await repository.AddAsync(exercise, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(exercise.Id);
    }
}
