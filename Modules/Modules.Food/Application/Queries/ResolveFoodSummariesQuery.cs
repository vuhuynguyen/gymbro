using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.DTOs;

namespace Modules.FoodModule.Application.Queries;

/// <summary>
/// Maps a set of food ids to their snapshot summaries (O(1) lookup). Internal cross-module read used by the
/// Nutrition module to denormalize food data onto plan/log items. Mirrors <c>ResolveExerciseNamesQuery</c>.
/// </summary>
public sealed record ResolveFoodSummariesQuery(IReadOnlyList<Guid> FoodIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, FoodSummaryDto>>>;
