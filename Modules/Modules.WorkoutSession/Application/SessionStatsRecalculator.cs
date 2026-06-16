using BuildingBlocks.Shared.Tracking;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application;

/// <summary>
/// Recomputes a session's denormalized read-model stats (currently <c>PrCount</c>). Used both when a session
/// is completed and when a <b>completed</b> session is later edited in place (fixing/adding sets or exercises),
/// so the stored PR count can't drift from what the Progress page derives at read time. e1RM/volume are
/// computed at read time, so they self-correct — only the cached PR count needs refreshing.
/// </summary>
public static class SessionStatsRecalculator
{
    /// <summary>
    /// Counts how many of the session's lifts set a new e1RM personal record versus the trainee's prior
    /// history (sessions started before this one). PR eligibility is single-sourced in <see cref="SessionPrRules"/>
    /// so the list count, the detail view and the Progress page agree.
    /// </summary>
    public static async Task<int> ComputePrCountAsync(
        IWorkoutSessionRepository sessionRepository,
        WorkoutSession session,
        IReadOnlyCollection<PerformedExercise> exercises,
        CancellationToken cancellationToken)
    {
        // Session-best e1RM per PR-eligible lift, from the working sets logged.
        var sessionBest = exercises
            .SelectMany(e => e.Sets
                .Where(s => SessionPrRules.IsPrEligibleSet(e.TrackingType, s))
                .Select(s => new { e.ExerciseId, E1 = s.EstimatedOneRepMaxKg!.Value }))
            .GroupBy(x => x.ExerciseId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.E1));

        if (sessionBest.Count == 0)
            return 0;

        // Only the lifts performed in THIS session can earn a PR, so bound the history scan to those ids.
        var prExerciseIds = sessionBest.Keys.ToList();

        // Prior best e1RM per exercise across earlier sessions, lifetime + cross-gym (QueryOwnAcrossGyms),
        // aggregated in SQL. Eligibility predicate mirrors SessionPrRules (not callable in a SQL projection).
        var priorBest = await sessionRepository.QueryOwnAcrossGyms(session.TraineeId)
            .Where(s => s.Id != session.Id && s.StartedAt < session.StartedAt)
            .SelectMany(s => s.Exercises)
            .Where(e => e.TrackingType == ExerciseTrackingType.Strength
                || e.TrackingType == ExerciseTrackingType.Bodyweight)
            .SelectMany(e => e.Sets.Select(set => new { e.ExerciseId, set.SetType, set.Reps, set.EstimatedOneRepMaxKg }))
            .Where(x => x.SetType == PerformedSetType.Working && x.EstimatedOneRepMaxKg != null
                && x.Reps != null && x.Reps <= SessionPrRules.MaxPrReps
                && prExerciseIds.Contains(x.ExerciseId))
            .GroupBy(x => x.ExerciseId)
            .Select(g => new { ExerciseId = g.Key, Best = g.Max(x => x.EstimatedOneRepMaxKg!.Value) })
            .ToDictionaryAsync(x => x.ExerciseId, x => x.Best, cancellationToken);

        return sessionBest.Count(kvp =>
            !priorBest.TryGetValue(kvp.Key, out var prior) || kvp.Value > prior);
    }

    /// <summary>
    /// After an edit to a session, refresh its cached PR count — only when it's <b>completed</b> (in-progress
    /// sessions are finalized on Complete, so there's nothing to refresh). Reads fresh state, so call this
    /// AFTER the mutation's SaveChanges, then SaveChanges again to persist the new count.
    /// </summary>
    public static async Task RecomputeAfterEditAsync(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        WorkoutSession session,
        CancellationToken cancellationToken)
    {
        if (session.Status != SessionStatus.Completed)
            return;

        var exercises = await exerciseRepository.Query()
            .Include(e => e.Sets)
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        var prCount = await ComputePrCountAsync(sessionRepository, session, exercises, cancellationToken);
        session.RecountPrs(prCount);
    }
}
