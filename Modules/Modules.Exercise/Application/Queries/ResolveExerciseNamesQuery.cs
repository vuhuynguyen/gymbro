using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.ExerciseModule.Application.Queries;

public sealed record ResolveExerciseNamesQuery(IReadOnlyList<Guid> ExerciseIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, string>>>;
