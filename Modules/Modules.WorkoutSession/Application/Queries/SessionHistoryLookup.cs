using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries;

/// <summary>
/// Read-only "last time" lookup: for the lifts in an in-progress session, the trainee's most recent prior
/// performance of each. Computed live (never stored) so it always reflects current history.
/// </summary>
internal static class SessionHistoryLookup
{
    private static readonly IReadOnlyDictionary<Guid, LastPerformedSetDto> Empty =
        new Dictionary<Guid, LastPerformedSetDto>();

    /// <summary>
    /// For each id in <paramref name="exerciseIds"/>, the top working set (by estimated 1RM, tie-broken on
    /// weight) the trainee logged in their most recent COMPLETED session started before <paramref name="before"/>.
    /// </summary>
    /// <param name="ownSessions">
    /// The caller's own sessions across all gyms (<c>QueryOwnAcrossGyms</c>) — "last time" follows the user,
    /// not the current gym.
    /// </param>
    public static async Task<IReadOnlyDictionary<Guid, LastPerformedSetDto>> TopWorkingSetPerExerciseAsync(
        IQueryable<WorkoutSession> ownSessions,
        IReadOnlyCollection<Guid> exerciseIds,
        DateTimeOffset before,
        CancellationToken cancellationToken)
    {
        if (exerciseIds.Count == 0)
            return Empty;

        // Bounded to the handful of lifts in the current session and to working sets that carry both
        // weight and reps. Materialize the candidates, then reduce in memory: pick each exercise's most
        // recent prior session, and within it the heaviest set. The reduction is trivial; the scan is
        // bounded by the current session's exercise count, not by total training history.
        var ids = exerciseIds.ToList();
        var rows = await ownSessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Completed && s.StartedAt < before)
            .SelectMany(s => s.Exercises
                .Where(e => ids.Contains(e.ExerciseId))
                .SelectMany(e => e.Sets
                    .Where(set => set.SetType == PerformedSetType.Working
                        && set.WeightKg != null && set.Reps != null)
                    .Select(set => new
                    {
                        e.ExerciseId,
                        SessionStartedAt = s.StartedAt,
                        set.WeightKg,
                        set.Reps,
                        E1 = set.EstimatedOneRepMaxKg
                    })))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ExerciseId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latestStart = g.Max(r => r.SessionStartedAt);
                    var best = g
                        .Where(r => r.SessionStartedAt == latestStart)
                        .OrderByDescending(r => r.E1 ?? 0m)
                        .ThenByDescending(r => r.WeightKg ?? 0m)
                        .First();
                    return new LastPerformedSetDto(best.WeightKg, best.Reps, latestStart);
                });
    }
}
