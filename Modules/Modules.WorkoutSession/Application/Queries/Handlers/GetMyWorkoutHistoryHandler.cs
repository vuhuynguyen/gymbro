using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// Unified personal workout history. Reads only the caller's own sessions across every gym via
/// <see cref="IWorkoutSessionRepository.QueryOwnAcrossGyms"/> (scoped to <c>currentUser.UserId</c>),
/// so there is no tenant context and no cross-user surface. Program/coach context is intentionally
/// omitted (cross-gym), so the workout name comes from the session's own stored snapshot name.
/// </summary>
public sealed class GetMyWorkoutHistoryHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyWorkoutHistoryQuery, Result<SessionListDto>>
{
    public async Task<Result<SessionListDto>> Handle(
        GetMyWorkoutHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);

        var query = sessionRepository.QueryOwnAcrossGyms(currentUser.UserId).AsNoTracking();

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        if (request.From.HasValue)
        {
            var fromUtc = request.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(s => s.StartedAt >= fromUtc);
        }

        if (request.To.HasValue)
        {
            var toUtc = request.To.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(s => s.StartedAt <= toUtc);
        }

        var total = await query.CountAsync(cancellationToken);

        // One round-trip: page the sessions and compute per-session counts/volume via correlated
        // subqueries. Filters are off (QueryOwnAcrossGyms), so child rows in any gym are included.
        var rows = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                Session = s,
                TotalExercises = s.Exercises.Count(),
                // Drop/rest-pause stages roll up into their lead set — count only parentless rows.
                TotalSets = s.Exercises.SelectMany(e => e.Sets).Count(set => set.ParentSetId == null),
                Volume = s.Exercises
                    .SelectMany(e => e.Sets)
                    .Where(set => set.SetType == PerformedSetType.Working
                        && set.WeightKg != null && set.Reps != null)
                    .Sum(set => (decimal?)(set.WeightKg!.Value * set.Reps!.Value)) ?? 0m
            })
            .ToListAsync(cancellationToken);

        // Cross-gym program context (name + week + weekly goal) for the plan-based sessions on this page.
        var assignmentIds = rows
            .Where(r => r.Session.PlanAssignmentId.HasValue)
            .Select(r => r.Session.PlanAssignmentId!.Value)
            .Distinct()
            .ToList();

        var planContext = await ResolvePlanContextAsync(assignmentIds, cancellationToken);

        var items = rows
            .Select(r =>
            {
                var ctx = r.Session.PlanAssignmentId.HasValue
                    && planContext.TryGetValue(r.Session.PlanAssignmentId.Value, out var pc)
                        ? pc
                        : null;
                return SessionMapping.ToSessionSummaryDto(
                    r.Session,
                    r.TotalSets,
                    r.TotalExercises,
                    r.Volume,
                    r.Session.PrCount,
                    ctx?.ProgramName,
                    ctx is null ? null : SessionMapping.ComputePlanWeek(ctx.StartDate, r.Session.StartedAt),
                    ctx?.FrequencyDaysPerWeek);
            })
            .ToList();

        return Result<SessionListDto>.Success(
            SessionMapping.ToSessionListDto(items, page, pageSize, total));
    }

    private async Task<IReadOnlyDictionary<Guid, PlanContextDto>> ResolvePlanContextAsync(
        IReadOnlyList<Guid> assignmentIds, CancellationToken cancellationToken)
    {
        if (assignmentIds.Count == 0)
            return new Dictionary<Guid, PlanContextDto>();

        var result = await mediator.Send(
            new ResolveOwnPlanContextQuery(assignmentIds, currentUser.UserId), cancellationToken);
        return result.IsSuccess ? result.Value! : new Dictionary<Guid, PlanContextDto>();
    }
}
