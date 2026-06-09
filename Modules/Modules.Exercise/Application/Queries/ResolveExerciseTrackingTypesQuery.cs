using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;

namespace Modules.ExerciseModule.Application.Queries;

/// <summary>
/// Resolves the <see cref="ExerciseTrackingType"/> for the given exercises. Consumed by the WorkoutSession module so it
/// can denormalize the tracking mode onto a performed exercise (alongside the captured name) — letting the loggers and
/// the per-mode validation work without a per-log cross-module lookup. Mirrors <see cref="ResolveExerciseNamesQuery"/>.
/// </summary>
public sealed record ResolveExerciseTrackingTypesQuery(IReadOnlyList<Guid> ExerciseIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>>;
