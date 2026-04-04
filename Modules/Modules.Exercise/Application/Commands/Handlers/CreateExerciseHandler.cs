using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Commands.Handlers;

public class CreateExerciseHandler(
    IExerciseRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateExerciseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateExerciseCommand request,
        CancellationToken cancellationToken)
    {
        // validate enum
        if (!Enum.TryParse<MuscleGroup>(request.MuscleGroup, out var muscle))
        {
            return Result<Guid>.Failure(
                Error.Validation("Invalid muscle group")
            );
        }

        // check duplicate
        var exists = await repository.Query()
            .AnyAsync(x => x.DefaultName == request.Name, cancellationToken);

        if (exists)
        {
            return Result<Guid>.Failure(
                Error.Conflict("Exercise already exists")
            );
        }

        // create domain entity
        var exercise = Exercise.CreateGlobal(
            request.Name,
            muscle,
            request.ImageUrl ?? "",
            request.Description
        );

        // persist
        await repository.AddAsync(exercise, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // return result
        return Result<Guid>.Success(exercise.Id);
    }
}