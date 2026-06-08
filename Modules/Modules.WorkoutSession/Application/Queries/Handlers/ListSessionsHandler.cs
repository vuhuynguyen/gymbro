using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

public sealed class ListSessionsHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IMediator mediator,
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<ListSessionsQuery, Result<SessionListDto>>
{
    public async Task<Result<SessionListDto>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == null)
            return Result<SessionListDto>.Failure(Validation("TenantId", "X-Tenant-Id header is required."));

        var tenantId = tenantContext.TenantId.Value;

        bool canViewAll = await tenantAuth.HasPermissionAsync(tenantId, Permission.WorkoutLogViewAll, cancellationToken);

        // Resolve the trainee being requested: a ViewAll caller may target any trainee in the tenant;
        // everyone else is implicitly scoped to themselves. Gate that effective id through the same
        // per-trainee check the report handlers use, so a caller without ViewAll cannot read another
        // trainee's sessions by supplying request.TraineeId.
        var requestedTraineeId = canViewAll ? (request.TraineeId ?? currentUser.UserId) : currentUser.UserId;

        // The list is tenant-filtered, so the resource tenant is the active tenant: a ViewAll caller is
        // bounded to their own gym (explicit, no longer implicit via the EF filter alone).
        if (!await ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync(
                tenantAuth, tenantId, requestedTraineeId, tenantId, cancellationToken))
            return Result<SessionListDto>.Failure(Unauthorized("Unauthorized", "You cannot view these workout logs."));

        var query = sessionRepository.Query();

        // Scope to current user unless they have ViewAll permission
        var effectiveTraineeId = canViewAll ? request.TraineeId : currentUser.UserId;
        if (effectiveTraineeId.HasValue)
            query = query.Where(s => s.TraineeId == effectiveTraineeId.Value);
        else if (!canViewAll)
            query = query.Where(s => s.TraineeId == currentUser.UserId);

        if (request.PlanAssignmentId.HasValue)
            query = query.Where(s => s.PlanAssignmentId == request.PlanAssignmentId.Value);

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

        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var sessionIds = sessions.Select(s => s.Id).ToList();

        var exerciseCounts = await exerciseRepository.Query()
            .Where(e => sessionIds.Contains(e.SessionId))
            .GroupBy(e => e.SessionId)
            .Select(g => new { g.Key, Count = g.Count(), Sets = g.Sum(e => e.Sets.Count) })
            .ToListAsync(cancellationToken);

        var countMap = exerciseCounts.ToDictionary(x => x.Key);

        // Per-session working-set volume (Σ weight × reps), as a flat aggregate that SQL can translate.
        var volumeRows = await exerciseRepository.Query()
            .Where(e => sessionIds.Contains(e.SessionId))
            .SelectMany(e => e.Sets.Select(set => new { e.SessionId, set.SetType, set.WeightKg, set.Reps }))
            .Where(x => x.SetType == PerformedSetType.Working && x.WeightKg != null && x.Reps != null)
            .GroupBy(x => x.SessionId)
            .Select(g => new { g.Key, Volume = g.Sum(x => x.WeightKg!.Value * x.Reps!.Value) })
            .ToListAsync(cancellationToken);

        var volumeMap = volumeRows.ToDictionary(x => x.Key, x => x.Volume);

        // Program context (name + start date) for the plan assignments referenced on this page.
        var assignmentIds = sessions
            .Where(s => s.PlanAssignmentId.HasValue)
            .Select(s => s.PlanAssignmentId!.Value)
            .Distinct()
            .ToList();

        var planContext = await ResolvePlanContextAsync(assignmentIds, cancellationToken);

        var items = sessions
            .Select(s =>
            {
                var ctx = s.PlanAssignmentId.HasValue
                    && planContext.TryGetValue(s.PlanAssignmentId.Value, out var pc)
                        ? pc
                        : null;
                return SessionMapping.ToSessionSummaryDto(
                    s,
                    countMap.TryGetValue(s.Id, out var c) ? c.Sets : 0,
                    countMap.TryGetValue(s.Id, out var ce) ? ce.Count : 0,
                    volumeMap.TryGetValue(s.Id, out var vol) ? vol : 0m,
                    s.PrCount,
                    ctx?.ProgramName,
                    ctx is null ? null : SessionMapping.ComputePlanWeek(ctx.StartDate, s.StartedAt),
                    ctx?.FrequencyDaysPerWeek);
            })
            .ToList();

        return Result<SessionListDto>.Success(
            SessionMapping.ToSessionListDto(items, request.Page, request.PageSize, total));
    }

    private async Task<IReadOnlyDictionary<Guid, Modules.WorkoutPlanModule.Application.DTOs.PlanContextDto>>
        ResolvePlanContextAsync(IReadOnlyList<Guid> assignmentIds, CancellationToken cancellationToken)
    {
        if (assignmentIds.Count == 0)
            return new Dictionary<Guid, Modules.WorkoutPlanModule.Application.DTOs.PlanContextDto>();

        var result = await mediator.Send(new ResolvePlanContextQuery(assignmentIds), cancellationToken);
        return result.IsSuccess
            ? result.Value!
            : new Dictionary<Guid, Modules.WorkoutPlanModule.Application.DTOs.PlanContextDto>();
    }
}
