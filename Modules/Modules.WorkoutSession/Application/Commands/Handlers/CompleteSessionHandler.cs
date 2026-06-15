using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class CompleteSessionHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CompleteSessionCommand, Result<CompleteSessionResultDto>>
{
    public async Task<Result<CompleteSessionResultDto>> Handle(
        CompleteSessionCommand request,
        CancellationToken cancellationToken)
    {
        var load = await SessionGuard.LoadOwnedInProgressAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken,
            SessionGuard.TerminalStateMessage);
        if (load.IsFailure)
            return Result<CompleteSessionResultDto>.Failure(load.Error);
        var session = load.Value!;

        var exercises = await exerciseRepository.Query()
            .Include(e => e.Sets)
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        foreach (var ex in exercises)
            ex.MarkCompleted();

        var prCount = await ComputePrCountAsync(session, exercises, cancellationToken);

        session.Complete(request.RpeOverall, request.Notes, request.CompletedAt, prCount);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Drop/rest-pause stages roll up into their lead set — count only parentless rows.
        var totalSets = exercises.Sum(e => e.Sets.Count(s => s.ParentSetId == null));
        var totalExercises = exercises.Count;

        return Result<CompleteSessionResultDto>.Success(
            SessionMapping.ToCompleteSessionResultDto(session, totalSets, totalExercises));
    }

    /// <summary>
    /// Counts how many exercises in the completing session set a new e1RM personal record versus the
    /// trainee's prior history. Finalized here (read-model) so the session list never re-walks full
    /// history per page. A trainee has at most one open session, so the completing session is always the
    /// latest by <c>StartedAt</c> — its PR count equals the chronological running-max result for it.
    /// </summary>
    private async Task<int> ComputePrCountAsync(
        WorkoutSession session,
        IReadOnlyCollection<PerformedExercise> exercises,
        CancellationToken cancellationToken)
    {
        // Session-best e1RM per PR-eligible lift, from the working sets just logged (already in memory).
        // Eligibility (working set, strength/bodyweight, reps ≤ 12, e1RM present) is single-sourced in
        // SessionPrRules so the list count, the detail view and the Progress page agree.
        var sessionBest = exercises
            .SelectMany(e => e.Sets
                .Where(s => SessionPrRules.IsPrEligibleSet(e.TrackingType, s))
                .Select(s => new { e.ExerciseId, E1 = s.EstimatedOneRepMaxKg!.Value }))
            .GroupBy(x => x.ExerciseId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.E1));

        if (sessionBest.Count == 0)
            return 0;

        // Only the lifts performed in THIS session can earn a PR, so bound the history scan to those
        // exercise ids. Without this filter the aggregation groups every exercise the trainee has ever
        // done and discards the irrelevant ones in memory — work that grows with training history and
        // becomes a read hotspot for long-tenured users. Result is identical.
        var prExerciseIds = sessionBest.Keys.ToList();

        // Prior best e1RM per exercise across sessions started before this one, aggregated in SQL so only
        // one row per exercise is materialized. Read the trainee's own LIFETIME history across all gyms
        // (QueryOwnAcrossGyms) — matching the detail view and the documented "all earlier sessions"
        // semantics — so a multi-gym trainee's stored PrCount can't disagree with the recomputed detail.
        // The eligibility predicate mirrors SessionPrRules (not callable in a SQL projection).
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
}
