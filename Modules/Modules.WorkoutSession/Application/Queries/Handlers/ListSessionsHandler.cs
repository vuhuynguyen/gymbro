using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

public sealed class ListSessionsHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
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

        if (!await ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync(
                tenantAuth, tenantId, requestedTraineeId, cancellationToken))
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

        var items = sessions
            .Select(s => SessionMapping.ToSessionSummaryDto(
                s,
                countMap.TryGetValue(s.Id, out var c) ? c.Sets : 0,
                countMap.TryGetValue(s.Id, out var ce) ? ce.Count : 0))
            .ToList();

        return Result<SessionListDto>.Success(
            SessionMapping.ToSessionListDto(items, request.Page, request.PageSize, total));
    }
}
