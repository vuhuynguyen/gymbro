using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

public sealed class GetSessionByIdHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<GetSessionByIdQuery, Result<SessionDetailDto>>
{
    public async Task<Result<SessionDetailDto>> Handle(
        GetSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == null)
            return Result<SessionDetailDto>.Failure(Validation("TenantId", "X-Tenant-Id header is required."));

        var tenantId = tenantContext.TenantId.Value;

        var session = await sessionRepository.GetWithExercisesAndSetsAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result<SessionDetailDto>.Failure(NotFound("NotFound", "Session not found."));

        if (!await ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync(
                tenantAuth, tenantId, session.TraineeId, cancellationToken))
            return Result<SessionDetailDto>.Failure(Unauthorized("Unauthorized", "You cannot view this session."));

        var snapshotDto = SessionMapping.DeserializeSnapshot(session.SnapshotJson);

        // HideSetsReps (filter-on-read): only the owning trainee's own view is redacted; a coach
        // viewing via WorkoutLogViewAll (and admins) always sees the full prescription. Querying the
        // assignment only on the owner path also stays within its row-level guard.
        if (snapshotDto != null
            && !currentUser.IsAdmin
            && session.TraineeId == currentUser.UserId
            && session.PlanAssignmentId is { } hideAssignmentId)
        {
            var hideResult = await mediator.Send(
                new GetPlanAssignmentByIdQuery(hideAssignmentId), cancellationToken);
            if (hideResult.IsSuccess && hideResult.Value!.HideSetsReps)
                snapshotDto = SessionMapping.RedactSnapshotTargets(snapshotDto);
        }

        var exerciseIds = SessionMapping.CollectExerciseIds(session.Exercises);
        var namesResult = await mediator.Send(new ResolveExerciseNamesQuery(exerciseIds), cancellationToken);
        if (namesResult.IsFailure)
            return Result<SessionDetailDto>.Failure(namesResult.Error);

        // Render planned exercises with the names captured in the snapshot at session start.
        var names = SessionMapping.MergeSnapshotNames(namesResult.Value!, snapshotDto);

        // Prior best e1RM per lift for this trainee, from sessions that started before this one. Bounded to
        // the exercises in THIS session (the only lifts DetectPrs can flag) so the history aggregation does
        // not grow with the trainee's full catalog of past exercises. Result is identical.
        var priorBest = await sessionRepository.Query()
            .Where(s => s.TraineeId == session.TraineeId
                && s.Id != session.Id
                && s.StartedAt < session.StartedAt)
            .SelectMany(s => s.Exercises)
            .SelectMany(e => e.Sets.Select(set => new { e.ExerciseId, set.SetType, set.EstimatedOneRepMaxKg }))
            .Where(x => x.SetType == PerformedSetType.Working && x.EstimatedOneRepMaxKg != null
                && exerciseIds.Contains(x.ExerciseId))
            .GroupBy(x => x.ExerciseId)
            .Select(g => new { ExerciseId = g.Key, Best = g.Max(x => x.EstimatedOneRepMaxKg!.Value) })
            .ToDictionaryAsync(x => x.ExerciseId, x => x.Best, cancellationToken);

        var (prSetIds, prs) = SessionMapping.DetectPrs(session.Exercises, priorBest, names);

        // Program context (name + week) when the session is tied to a plan assignment.
        string? programName = null;
        int? planWeek = null;
        if (session.PlanAssignmentId is { } assignmentId)
        {
            var contextResult = await mediator.Send(
                new ResolvePlanContextQuery([assignmentId]), cancellationToken);
            if (contextResult.IsSuccess && contextResult.Value!.TryGetValue(assignmentId, out var ctx))
            {
                programName = ctx.ProgramName;
                planWeek = SessionMapping.ComputePlanWeek(ctx.StartDate, session.StartedAt);
            }
        }

        return Result<SessionDetailDto>.Success(
            SessionMapping.ToSessionDetailDto(
                session, snapshotDto, names, prSetIds, prs, programName, planWeek));
    }
}
