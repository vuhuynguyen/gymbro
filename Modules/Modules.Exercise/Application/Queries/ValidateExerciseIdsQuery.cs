using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.ExerciseModule.Application.Queries;

public sealed record ValidateExerciseIdsQuery(IReadOnlyList<Guid> ExerciseIds)
    : IRequest<Result>;
