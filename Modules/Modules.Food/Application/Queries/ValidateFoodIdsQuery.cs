using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.FoodModule.Application.Queries;

/// <summary>Validates that referenced food ids exist (mirrors <c>ValidateExerciseIdsQuery</c>).</summary>
public sealed record ValidateFoodIdsQuery(IReadOnlyList<Guid> FoodIds)
    : IRequest<Result>;
