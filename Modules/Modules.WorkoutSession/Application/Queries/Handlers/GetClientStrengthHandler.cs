using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// One client's top-lift e1RM trends for the coach detail view (api/clients/{id}/progress/strength, Phase 2b),
/// TENANT-SCOPED to the active gym.
///
/// <para>R2 isolation (the single most dangerous item): this is a SEPARATE handler from the trainee
/// <c>GetMyExerciseE1rmSeriesHandler</c>. It reads through <see cref="IWorkoutSessionRepository.Query"/> with
/// the EF tenant filter ON — so a client who trains in multiple gyms is seen here ONLY by their in-gym
/// sessions. It NEVER calls <c>QueryOwnAcrossGyms</c> and is NEVER parameterized by the trainee id over the
/// self-scoped path. <c>WorkoutLogViewAll</c> + <see cref="ResourceAccessGuard"/> confirm the trainee is a
/// member of the active tenant; a non-member yields a failure (403), never a silent rescope to self.</para>
///
/// Applies the SAME honesty gate as the trainee series (Working ∧ e1RM ∧ reps ≤ 12 ∧ Strength/Bodyweight),
/// reduces to one MAX point per session, selects the most-trained lifts (bounded by <c>Take</c>, requiring
/// ≥<see cref="MinSessionsForTopLift"/> qualifying sessions), and reuses <see cref="E1rmSeriesCalculator"/>
/// for the Current/Delta/Direction/Stall/Spark summary — so the coach view and the trainee view agree by
/// construction (only the scope differs). Returns an empty list when the client has no qualifying in-gym lifts.
/// </summary>
public sealed class GetClientStrengthHandler(
    IWorkoutSessionRepository sessionRepository,
    ITenantAuthorizationService tenantAuth,
    ITenantRoleResolver roleResolver,
    ITenantContext tenantContext,
    IMediator mediator)
    : IRequestHandler<GetClientStrengthQuery, Result<IReadOnlyList<LiftTrendDto>>>
{
    private const int WindowWeeks = 12;
    private const int MinSessionsForTopLift = 4;   // < 4 sessions ⇒ direction is noise, omit (matches overview)
    private const int MaxTakeCeiling = 12;         // hard cap so a hostile `take` can't fan out the read
    // Spark-point cap lives in E1rmSeriesCalculator.DefaultSparkPoints (the shared default).

    public async Task<Result<IReadOnlyList<LiftTrendDto>>> Handle(
        GetClientStrengthQuery request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
            return Result<IReadOnlyList<LiftTrendDto>>.Failure(
                Validation("TenantId", "X-Tenant-Id header is required."));

        // Coach gate (caller side): only a WorkoutLogViewAll caller bound to THIS gym may read another
        // client; a plain member is denied (and CanAccessResourceAsync would only ever let them read their
        // OWN id — never a silent rescope to a foreign trainee).
        if (!await ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync(
                tenantAuth, tenantId, request.TraineeId, tenantId, cancellationToken))
            return Result<IReadOnlyList<LiftTrendDto>>.Failure(
                Unauthorized("Unauthorized", "You cannot view this client's strength progress."));

        // Membership row-level check (subject side): the caller gate above confirms the COACH's own gym,
        // but the requested trainee must ALSO be a member of the active tenant. A non-member id resolves to
        // no role here → 404 (cross-tenant invisibility), never a silent rescope to self or an empty 200.
        if (await roleResolver.GetRoleAsync(request.TraineeId, tenantId, cancellationToken) is null)
            return Result<IReadOnlyList<LiftTrendDto>>.Failure(
                NotFound("NotFound", "This client is not a member of the gym."));

        var take = Math.Clamp(request.Take, 1, MaxTakeCeiling);

        // Bucket the client's weeks in the CLIENT's zone (never the coach's).
        var clientZone = await mediator.Send(new GetUserTimeZoneQuery(request.TraineeId), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var currentWeekStart = LocalDayResolver.WeekStartOf(now, clientZone);
        var windowStart = currentWeekStart.AddDays(-7 * (WindowWeeks - 1));
        var windowLowerBoundUtc = LocalDayResolver.StartOfLocalDayUtc(windowStart, clientZone).AddDays(-1);

        // TENANT-FILTERED read (filter ON): the client's completed sessions in THIS gym, in window, with their
        // qualifying working sets projected. The honesty gate lives here in SQL; MAX-per-session in memory.
        var raw = await sessionRepository.Query()
            .Where(s => s.Status == SessionStatus.Completed
                && s.TraineeId == request.TraineeId
                && s.StartedAt >= windowLowerBoundUtc)
            .SelectMany(s => s.Exercises
                .Where(e => e.TrackingType == ExerciseTrackingType.Strength
                    || e.TrackingType == ExerciseTrackingType.Bodyweight)
                .Select(e => new
                {
                    s.StartedAt,
                    s.ClientTimezone,
                    e.ExerciseId,
                    e.ExerciseName,
                    e.TrackingType,
                    BestE1rmKg = e.Sets
                        .Where(set => set.SetType == PerformedSetType.Working
                            && set.EstimatedOneRepMaxKg != null
                            && set.WeightKg != null
                            && set.Reps != null && set.Reps <= 12)
                        .Max(set => (decimal?)set.EstimatedOneRepMaxKg)
                }))
            .ToListAsync(cancellationToken);

        // One e1RM point per (lift, session); drop sessions that contributed no qualifying set on the lift.
        var points = raw
            .Where(r => r.BestE1rmKg is not null)
            .Select(r => new
            {
                r.ExerciseId,
                r.ExerciseName,
                r.TrackingType,
                E1rmKg = r.BestE1rmKg!.Value,
                r.StartedAt,
                WeekStart = LocalDayResolver.WeekStartOf(r.StartedAt, r.ClientTimezone ?? clientZone)
            })
            .Where(p => p.WeekStart >= windowStart && p.WeekStart <= currentWeekStart)
            .ToList();

        // Most-trained lifts first, requiring ≥4 qualifying sessions, bounded by `take`.
        var topExerciseIds = points
            .GroupBy(p => p.ExerciseId)
            .Where(g => g.Count() >= MinSessionsForTopLift)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(take)
            .Select(g => g.Key)
            .ToList();

        var trends = new List<LiftTrendDto>(topExerciseIds.Count);
        foreach (var exerciseId in topExerciseIds)
        {
            var series = points
                .Where(p => p.ExerciseId == exerciseId)
                .OrderBy(p => p.StartedAt)
                .ToList();

            var calcPoints = series
                .Select((p, i) => new E1rmSeriesCalculator.Point(p.WeekStart, i, p.E1rmKg))
                .ToList();

            var trend = E1rmSeriesCalculator.Compute(calcPoints);
            var spark = E1rmSeriesCalculator.Spark(calcPoints);

            trends.Add(new LiftTrendDto(
                exerciseId,
                series[^1].ExerciseName,
                series[^1].TrackingType.ToString(),
                trend.CurrentE1rmKg,
                trend.DeltaKgVsTrailing4w,
                trend.Direction,
                trend.Stalled,
                trend.StallSessions,
                spark));
        }

        return Result<IReadOnlyList<LiftTrendDto>>.Success(trends);
    }
}
