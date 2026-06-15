using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

/// <summary>
/// Resolves the PRIMARY muscle group per exercise id, returned as a camelCase string so the value can cross
/// into WorkoutSession without exposing this module's <c>MuscleGroup</c> entity enum (keeps the module-boundary
/// contract intact). One batched, untracked read over the id set (no N+1); ids that map to no exercise — or to
/// an exercise with no primary muscle — are simply absent from the map.
/// </summary>
public sealed class ResolveExerciseMuscleGroupsHandler(IExerciseRepository repository)
    : IRequestHandler<ResolveExerciseMuscleGroupsQuery, Result<IReadOnlyDictionary<Guid, string>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, string>>> Handle(
        ResolveExerciseMuscleGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var ids = request.ExerciseIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result<IReadOnlyDictionary<Guid, string>>.Success(
                new Dictionary<Guid, string>());

        // Project (Id, primary-muscle markers) and read untracked — one batched query, no full Exercise
        // materialization and no per-id round trip. The primary/secondary reduction is done in memory because
        // the IsPrimary-preferring pick doesn't translate cleanly inside a grouped projection. (Mirrors the
        // ResolveExerciseTrackingTypes shape.)
        var rows = await repository.Query()
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                Muscles = e.Muscles
                    .Select(m => new { m.Muscle, m.IsPrimary })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<Guid, string>(rows.Count);
        foreach (var row in rows)
        {
            // Prefer a primary muscle; fall back to any muscle so a (malformed) primary-less exercise still
            // resolves. No muscles at all ⇒ leave the id out of the map (the consumer surfaces null).
            var primary = row.Muscles
                .OrderByDescending(m => m.IsPrimary)
                .Select(m => (MuscleGroup?)m.Muscle)
                .FirstOrDefault();

            if (primary is MuscleGroup group)
                map[row.Id] = ToCamelCase(group);
        }

        return Result<IReadOnlyDictionary<Guid, string>>.Success(map);
    }

    // The 6-value MuscleGroup enum is single-word, so the camelCase wire form is just the lowercased name
    // (chest|back|legs|shoulders|arms|core) — matching the API's camelCase enum-string convention.
    private static string ToCamelCase(MuscleGroup group)
    {
        var name = group.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
