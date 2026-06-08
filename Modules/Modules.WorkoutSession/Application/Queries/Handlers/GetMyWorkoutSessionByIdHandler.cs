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

/// <summary>
/// Self-scoped detail for the unified history. The session is loaded only when its
/// <c>TraineeId == currentUser.UserId</c> (via <see cref="IWorkoutSessionRepository.QueryOwnAcrossGyms"/>),
/// so another user's session id resolves to NotFound — never a cross-user leak. Prior-best e1RM is
/// computed across ALL the caller's gyms, so PR flags reflect lifetime records. The coach's prescription
/// snapshot is intentionally omitted (a within-gym concern), which also sidesteps cross-gym redaction.
/// </summary>
public sealed class GetMyWorkoutSessionByIdHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyWorkoutSessionByIdQuery, Result<SessionDetailDto>>
{
    public async Task<Result<SessionDetailDto>> Handle(
        GetMyWorkoutSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var session = await sessionRepository.QueryOwnAcrossGyms(currentUser.UserId)
            .AsNoTracking()
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            return Result<SessionDetailDto>.Failure(NotFound("NotFound", "Session not found."));

        var exerciseIds = SessionMapping.CollectExerciseIds(session.Exercises);
        var namesResult = await mediator.Send(new ResolveExerciseNamesQuery(exerciseIds), cancellationToken);
        if (namesResult.IsFailure)
            return Result<SessionDetailDto>.Failure(namesResult.Error);

        var names = namesResult.Value!;

        // Lifetime prior-best e1RM per lift, across every gym, from sessions started before this one and
        // bounded to this session's exercises (the only lifts DetectPrs can flag).
        var priorBest = await sessionRepository.QueryOwnAcrossGyms(currentUser.UserId)
            .Where(s => s.Id != session.Id && s.StartedAt < session.StartedAt)
            .SelectMany(s => s.Exercises)
            .SelectMany(e => e.Sets.Select(set => new { e.ExerciseId, set.SetType, set.EstimatedOneRepMaxKg }))
            .Where(x => x.SetType == PerformedSetType.Working && x.EstimatedOneRepMaxKg != null
                && exerciseIds.Contains(x.ExerciseId))
            .GroupBy(x => x.ExerciseId)
            .Select(g => new { ExerciseId = g.Key, Best = g.Max(x => x.EstimatedOneRepMaxKg!.Value) })
            .ToDictionaryAsync(x => x.ExerciseId, x => x.Best, cancellationToken);

        var (prSetIds, prs) = SessionMapping.DetectPrs(session.Exercises, priorBest, names);

        // Cross-gym program context (name + week) when the session came from a plan assignment.
        string? programName = null;
        int? planWeek = null;
        if (session.PlanAssignmentId is { } assignmentId)
        {
            var contextResult = await mediator.Send(
                new ResolveOwnPlanContextQuery([assignmentId], currentUser.UserId), cancellationToken);
            if (contextResult.IsSuccess && contextResult.Value!.TryGetValue(assignmentId, out var ctx))
            {
                programName = ctx.ProgramName;
                planWeek = SessionMapping.ComputePlanWeek(ctx.StartDate, session.StartedAt);
            }
        }

        return Result<SessionDetailDto>.Success(
            SessionMapping.ToSessionDetailDto(
                session, snapshot: null, names, prSetIds, prs, programName, planWeek));
    }
}
