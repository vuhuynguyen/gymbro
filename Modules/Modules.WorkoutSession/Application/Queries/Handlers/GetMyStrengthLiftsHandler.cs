using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// The FULL strength-lift list (api/me/exercises/strength-lifts, Phase 2) — the uncapped sibling of the
/// overview's top-3 strip. Self-scoped via <see cref="IWorkoutSessionRepository.QueryOwnAcrossGyms"/>
/// (<c>currentUser.UserId</c> only) across every gym the caller trains in; completed sessions only. Gathers
/// the SAME windowed e1RM points the overview uses via the shared <see cref="StrengthLiftSeries"/> (and thus
/// the shared <c>E1rmSeriesCalculator</c> math — no duplication) but WITHOUT the ≥4-session/top-3 cap: ALL
/// performed strength lifts are returned. Each is enriched with its primary muscle group over
/// <see cref="ResolveExerciseMuscleGroupsQuery"/>. The honesty gate is applied as a FLAG, not a filter:
/// <c>HasTrend = (sessionCount ≥ 4)</c>; below that the direction/stall/spark stay at their defaults so a thin
/// lift never gets a fabricated direction. An optional primary-muscle filter narrows the list in memory after
/// resolution. Sorted by current e1RM descending. Returns 200 + empty list for a brand-new user (never 204).
/// </summary>
public sealed class GetMyStrengthLiftsHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyStrengthLiftsQuery, Result<StrengthLiftListDto>>
{
    private const int DefaultWindowWeeks = 12;
    private const int MinWindowWeeks = 4;
    private const int MaxWindowWeeks = 52;

    public async Task<Result<StrengthLiftListDto>> Handle(
        GetMyStrengthLiftsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var windowWeeks = Math.Clamp(request.Weeks ?? DefaultWindowWeeks, MinWindowWeeks, MaxWindowWeeks);
        var userZone = currentUser.TimeZoneId;
        var currentWeekStart = LocalDayResolver.WeekStartOf(now, userZone);
        var windowStart = currentWeekStart.AddDays(-7 * (windowWeeks - 1));

        // SHARED gathering: one bounded self-scoped read → one MAX e1RM point per (lift, session) in the window.
        var points = await StrengthLiftSeries.GatherAsync(
            sessionRepository.QueryOwnAcrossGyms(currentUser.UserId),
            windowStart,
            currentWeekStart,
            userZone,
            cancellationToken);

        // SHARED reduction (same E1rmSeriesCalculator math as the overview / drill-down). No cap.
        var trends = StrengthLiftSeries.ToTrends(points);
        if (trends.Count == 0)
            return Result<StrengthLiftListDto>.Success(new StrengthLiftListDto([]));

        // Cross-module enrichment: primary muscle group per lift (camelCase string; absent ⇒ null).
        var muscleResult = await mediator.Send(
            new ResolveExerciseMuscleGroupsQuery(trends.Select(t => t.ExerciseId).ToList()),
            cancellationToken);
        IReadOnlyDictionary<Guid, string> muscleByExercise = muscleResult.IsSuccess
            ? muscleResult.Value!
            : new Dictionary<Guid, string>();

        // Optional primary-muscle filter, applied IN MEMORY after resolution (camelCase, tolerant: an unknown
        // value yields no filter, see the controller). A lift with no resolved primary group never matches.
        var filter = NormalizeMuscleFilter(request.MuscleGroup);

        var lifts = trends
            .Select(t =>
            {
                muscleByExercise.TryGetValue(t.ExerciseId, out var primaryMuscle);

                // Honesty gate as a FLAG: only ≥4 qualifying sessions earn a direction/stall/spark. Below that,
                // the trend fields stay default (Flat/false/0/empty) — the client shows e1RM + count only.
                var hasTrend = t.SessionCount >= StrengthLiftSeries.MinSessionsForTrend;

                return new StrengthLiftDto(
                    t.ExerciseId,
                    t.ExerciseName,
                    primaryMuscle,
                    t.SessionCount,
                    t.Trend.CurrentE1rmKg,
                    hasTrend,
                    hasTrend ? t.Trend.Direction : LiftTrendDirection.Flat,
                    hasTrend && t.Trend.Stalled,
                    hasTrend ? t.Trend.StallSessions : 0,
                    hasTrend ? t.Spark : []);
            })
            .Where(l => filter is null || string.Equals(l.PrimaryMuscleGroup, filter, StringComparison.Ordinal))
            .OrderByDescending(l => l.CurrentE1rmKg)
            .ThenBy(l => l.ExerciseId)
            .ToList();

        return Result<StrengthLiftListDto>.Success(new StrengthLiftListDto(lifts));
    }

    // The resolver emits primary groups as lower-cased single words (chest|back|legs|shoulders|arms|core), so
    // the in-memory match is a case-insensitive trim normalized to the same lowercase form. The controller has
    // already dropped values that don't parse to a known group, so a non-null value here is always a real group.
    private static string? NormalizeMuscleFilter(string? muscleGroup)
    {
        if (string.IsNullOrWhiteSpace(muscleGroup))
            return null;

        return muscleGroup.Trim().ToLowerInvariant();
    }
}
