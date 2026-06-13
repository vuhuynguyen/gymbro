using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public sealed class ValidateExerciseIdsHandler(IExerciseRepository repository)
    : IRequestHandler<ValidateExerciseIdsQuery, Result>
{
    public async Task<Result> Handle(ValidateExerciseIdsQuery request, CancellationToken cancellationToken)
    {
        var ids = request.ExerciseIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result.Success();

        var existingIds = await repository.Query()
            .Where(e => ids.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var missing = ids.Except(existingIds).FirstOrDefault();
        if (missing != Guid.Empty)
            return Result.Failure(Validation("ExerciseId", $"Exercise {missing} was not found."));

        return Result.Success();
    }
}
