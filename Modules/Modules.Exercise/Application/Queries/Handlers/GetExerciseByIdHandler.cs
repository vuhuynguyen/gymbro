using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public class GetExerciseByIdHandler(IExerciseRepository repository)
    : IRequestHandler<GetExerciseByIdQuery, Result<ExerciseDetailDto>>
{
    public async Task<Result<ExerciseDetailDto>> Handle(
        GetExerciseByIdQuery request,
        CancellationToken cancellationToken)
    {
        var exercise = await repository.Query()
            .AsNoTracking()
            .Where(x => x.Id == request.Id && !x.IsDeleted)
            .Select(x => new ExerciseDetailDto
            {
                Id = x.Id,
                Name = x.DefaultName,
                Description = x.DefaultDescription,
                MuscleGroup = x.MuscleGroup.ToString(),

                Instructions = x.Instructions
                    .OrderBy<ExerciseInstruction, object>(i => i.StepOrder)
                    .Select(i => i.Content)
                    .ToList(),

                Tags = x.Tags
                    .Select(t => t.Tag)
                    .ToList(),

                MediaUrls = x.Media
                    .Select(m => m.Url)
                    .ToList(),

                Warnings = x.Warnings
                    .Select(w => w.Content)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return exercise == null
            ? Result<ExerciseDetailDto>.Failure(Error.NotFound("Exercise not found.")) 
            : Result<ExerciseDetailDto>.Success(exercise);
    }
}
