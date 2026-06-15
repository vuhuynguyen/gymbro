using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.ExerciseModule.Application.Queries;

/// <summary>
/// Resolves the PRIMARY <c>MuscleGroup</c> for the given exercises. Consumed by the WorkoutSession module so it
/// can label Progress strength lifts by primary muscle without referencing this module's <c>*.Entities</c>
/// (the 6-value <c>MuscleGroup</c> enum lives there). Mirrors <see cref="ResolveExerciseTrackingTypesQuery"/>.
///
/// <para><b>Boundary-safe representation.</b> The value crossing into WorkoutSession is the primary group as a
/// camelCase STRING (one of <c>chest|back|legs|shoulders|arms|core</c>) — NOT the enum — so the consumer never
/// touches <c>Modules.ExerciseModule.Entities</c> and <c>ModuleBoundaryConventionTests</c> stays green. An id
/// that maps to no exercise (or an exercise with no muscles) is simply absent from the map.</para>
/// </summary>
public sealed record ResolveExerciseMuscleGroupsQuery(IReadOnlyList<Guid> ExerciseIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, string>>>;
