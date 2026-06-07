using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

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
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result<CompleteSessionResultDto>.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result<CompleteSessionResultDto>.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result<CompleteSessionResultDto>.Failure(Conflict("Conflict", "Session is already completed or abandoned."));

        var exercises = await exerciseRepository.Query()
            .Include(e => e.Sets)
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        foreach (var ex in exercises)
            ex.MarkCompleted();

        var prCount = await ComputePrCountAsync(session, exercises, cancellationToken);

        session.Complete(request.RpeOverall, request.Notes, request.CompletedAt, prCount);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var totalSets = exercises.Sum(e => e.Sets.Count);
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
        // Session-best e1RM per exercise, from the working sets just logged (already in memory).
        var sessionBest = exercises
            .SelectMany(e => e.Sets
                .Where(s => s.SetType == PerformedSetType.Working && s.EstimatedOneRepMaxKg != null)
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

        // Prior best e1RM per exercise across sessions started before this one, aggregated in SQL so
        // only one row per exercise is materialized (mirrors GetSessionByIdHandler's priorBest).
        var priorBest = await sessionRepository.Query()
            .Where(s => s.TraineeId == session.TraineeId
                && s.Id != session.Id
                && s.StartedAt < session.StartedAt)
            .SelectMany(s => s.Exercises)
            .SelectMany(e => e.Sets.Select(set => new { e.ExerciseId, set.SetType, set.EstimatedOneRepMaxKg }))
            .Where(x => x.SetType == PerformedSetType.Working && x.EstimatedOneRepMaxKg != null
                && prExerciseIds.Contains(x.ExerciseId))
            .GroupBy(x => x.ExerciseId)
            .Select(g => new { ExerciseId = g.Key, Best = g.Max(x => x.EstimatedOneRepMaxKg!.Value) })
            .ToDictionaryAsync(x => x.ExerciseId, x => x.Best, cancellationToken);

        return sessionBest.Count(kvp =>
            !priorBest.TryGetValue(kvp.Key, out var prior) || kvp.Value > prior);
    }
}
