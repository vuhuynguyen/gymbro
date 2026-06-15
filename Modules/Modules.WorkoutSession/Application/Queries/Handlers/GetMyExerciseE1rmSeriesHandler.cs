using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// The per-lift e1RM drill-down (api/me/exercises/{id}/e1rm-series, Phase 2). Self-scoped via
/// <see cref="IWorkoutSessionRepository.QueryOwnAcrossGyms"/> (<c>currentUser.UserId</c> only) across every
/// gym the caller trains in; only completed sessions count. Applies the SAME honesty gate as the overview
/// (Working ∧ e1RM ∧ reps ≤ 12 ∧ Strength/Bodyweight) in SQL, reduces to ONE point per session
/// (MAX qualifying working-set e1RM, capturing the set that produced it), derives PR markers from the running
/// max, and reuses <see cref="E1rmSeriesCalculator"/> for the Current/Delta/Direction/Stall summary — so the
/// drill-down and the home sparkline agree by construction. From/to default to the trailing 12 weeks. Returns
/// an empty-but-valid DTO (200, never 404) for an unknown or never-trained lift.
/// </summary>
public sealed class GetMyExerciseE1rmSeriesHandler(
    IWorkoutSessionRepository sessionRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyExerciseE1rmSeriesQuery, Result<ExerciseE1rmSeriesDto>>
{
    private const int DefaultWindowWeeks = 12;

    public async Task<Result<ExerciseE1rmSeriesDto>> Handle(
        GetMyExerciseE1rmSeriesQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var userZone = currentUser.TimeZoneId;

        // Default window: trailing 12 weeks (Monday-anchored, in the trainee's zone) when no `from` is given.
        var defaultFrom = LocalDayResolver.WeekStartOf(now, userZone).AddDays(-7 * (DefaultWindowWeeks - 1));
        var fromLocal = request.From ?? defaultFrom;

        // Convert the local-day bounds to UTC instants in the trainee's stored zone (the tz claim, UTC
        // fallback) so an evening session west of UTC isn't excluded/leaked at the boundary. A conservative
        // one-day slack on the lower bound keeps the scan bounded; exact local-day bucketing is in memory.
        var fromUtc = LocalDayResolver.StartOfLocalDayUtc(fromLocal, userZone).AddDays(-1);
        DateTimeOffset? toExclusiveUtc = request.To.HasValue
            ? LocalDayResolver.StartOfLocalDayUtc(request.To.Value.AddDays(1), userZone).AddDays(1)
            : null;

        // One bounded read: every completed session that performed THIS lift in range, with its qualifying
        // working sets projected. The honesty gate lives here in SQL; MAX-per-session + PR markers in memory.
        var raw = await sessionRepository.QueryOwnAcrossGyms(currentUser.UserId)
            .Where(s => s.Status == SessionStatus.Completed
                && s.StartedAt >= fromUtc
                && (toExclusiveUtc == null || s.StartedAt < toExclusiveUtc))
            .SelectMany(s => s.Exercises
                .Where(e => e.ExerciseId == request.ExerciseId
                    && (e.TrackingType == ExerciseTrackingType.Strength
                        || e.TrackingType == ExerciseTrackingType.Bodyweight))
                .Select(e => new
                {
                    s.StartedAt,
                    s.ClientTimezone,
                    e.ExerciseName,
                    e.TrackingType,
                    // Qualifying working sets only (e1RM present, reps ≤ 12). Drop/AMRAP/failure stages carry
                    // null e1RM and are excluded by the != null filter, so the MAX is cluster-safe.
                    Sets = e.Sets
                        .Where(set => set.SetType == PerformedSetType.Working
                            && set.EstimatedOneRepMaxKg != null
                            && set.WeightKg != null
                            && set.Reps != null && set.Reps <= 12)
                        .Select(set => new
                        {
                            E1rmKg = set.EstimatedOneRepMaxKg!.Value,
                            WeightKg = set.WeightKg!.Value,
                            Reps = set.Reps!.Value
                        })
                        .ToList()
                }))
            .ToListAsync(cancellationToken);

        // Carry a stable display name/tracking type even when a session contributes no qualifying set (so an
        // unknown lift still reports null name + the default Strength tag, and a real lift keeps its snapshot).
        string? exerciseName = raw.Select(r => r.ExerciseName).FirstOrDefault(n => n is not null);
        var trackingType = raw.Count > 0 ? raw[0].TrackingType : ExerciseTrackingType.Strength;

        // One point per session = the session's MAX qualifying e1RM, plus the set that produced it. Re-bucket
        // each session to its own local day/week, then keep only points inside [fromLocal, to].
        var sessionPoints = raw
            .Where(r => r.Sets.Count > 0)
            .Select(r =>
            {
                var top = r.Sets.OrderByDescending(x => x.E1rmKg).First();
                return new
                {
                    LocalDate = LocalDayResolver.LocalDateOf(r.StartedAt, r.ClientTimezone ?? userZone),
                    WeekStart = LocalDayResolver.WeekStartOf(r.StartedAt, r.ClientTimezone ?? userZone),
                    r.StartedAt,
                    SessionBestE1rmKg = top.E1rmKg,
                    TopSetWeightKg = top.WeightKg,
                    TopSetReps = top.Reps
                };
            })
            .Where(p => p.LocalDate >= fromLocal && (request.To == null || p.LocalDate <= request.To.Value))
            .OrderBy(p => p.StartedAt)   // oldest → newest, so the running-max PR walk is chronological
            .ToList();

        // PR markers derived HERE from the series: a session is a PR when its best strictly exceeds the
        // running max so far (the first qualifying session is always a PR).
        var runningMax = decimal.MinValue;
        var points = new List<E1rmSeriesPointDto>(sessionPoints.Count);
        foreach (var p in sessionPoints)
        {
            var isPr = p.SessionBestE1rmKg > runningMax;
            if (isPr) runningMax = p.SessionBestE1rmKg;
            points.Add(new E1rmSeriesPointDto(
                p.LocalDate, p.SessionBestE1rmKg, p.TopSetWeightKg, p.TopSetReps, isPr));
        }

        // Reuse the shared calculator for the Current/Delta/Direction/Stall summary (same math as the overview).
        var calcPoints = sessionPoints
            .Select((p, i) => new E1rmSeriesCalculator.Point(p.WeekStart, i, p.SessionBestE1rmKg))
            .ToList();
        var trend = E1rmSeriesCalculator.Compute(calcPoints);

        return Result<ExerciseE1rmSeriesDto>.Success(new ExerciseE1rmSeriesDto(
            request.ExerciseId,
            exerciseName,
            trackingType.ToString(),
            points,
            trend.CurrentE1rmKg,
            trend.DeltaKgVsTrailing4w,
            trend.Direction,
            trend.Stalled,
            trend.StallSessions));
    }
}
