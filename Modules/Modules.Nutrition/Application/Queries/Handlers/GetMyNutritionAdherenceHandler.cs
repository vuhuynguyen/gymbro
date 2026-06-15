using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Queries.Handlers;

/// <summary>
/// The caller's nutrition-plan adherence trend (api/me/progress/nutrition-adherence, Phase 3), self-scoped to
/// currentUser.UserId across every gym via <see cref="IDailyNutritionLogRepository.QueryOwnAcrossGyms"/>.
/// Nutrition is a daily signal, so the default window is short — trailing <b>4 weeks</b> (vs. 12 for strength).
/// Only PLANNED days (<c>Source == FromAssignment</c>) feed the trend: an ad-hoc self-logged day carries no plan
/// to adhere to (its adherence is 100% by convention) and would inflate the trend, so it is excluded. Per-day
/// adherence reuses the SQL count projection (<see cref="NutritionMapping.SummaryRowProjection"/>): a closed
/// day reports its finalized <c>AdherencePct</c>, an open day a live recompute. <c>CurrentWeekAvgPct</c> is the
/// mean adherence over the current local week's planned days (null when none). <c>HasPlan=false</c> (empty
/// Days, null avg) only when the caller has NEVER had a planned nutrition day — a 200, never a 404 or 204.
/// <para>
/// Ad-hoc logging is honored as a SEPARATE tracking signal (Decision <b>D15</b>), not folded into the % above:
/// a second ALL-SOURCES read (no <c>Source</c> filter) yields <c>LoggedDaysThisWeek</c> — the current local
/// week's days that actually carry a logged item — and <c>HasAnyLogging</c> — whether the caller has EVER
/// logged one (a bounded EXISTS). A plan-less self-logger thus reads <c>HasPlan=false</c>, empty Days, yet
/// <c>LoggedDaysThisWeek &gt; 0</c> / <c>HasAnyLogging=true</c>, so their effort is counted without faking an
/// adherence record. Mirrors how workout sessions already count ad-hoc training.
/// </para>
/// Query-only: rides the existing <c>DailyNutritionLog.AdherencePct</c>; no new entity, no migration, no cache.
/// </summary>
public sealed class GetMyNutritionAdherenceHandler(
    IDailyNutritionLogRepository logRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyNutritionAdherenceQuery, Result<NutritionAdherenceDto>>
{
    private const int DefaultWindowWeeks = 4;

    public async Task<Result<NutritionAdherenceDto>> Handle(
        GetMyNutritionAdherenceQuery request,
        CancellationToken cancellationToken)
    {
        // The trainee's own stored zone anchors "today" and the current week; each day's own LocalDate is the
        // authoritative bucket below (it was already resolved in the caller's zone when the day was opened).
        var userZone = currentUser.TimeZoneId;
        var today = LocalDayResolver.LocalDateOf(DateTimeOffset.UtcNow, userZone);
        var to = request.To ?? today;
        // Default window: trailing 4 weeks ending at `to` (inclusive) when no `from` is given.
        var from = request.From ?? to.AddDays(-7 * DefaultWindowWeeks + 1);

        var currentWeekStart = LocalDayResolver.MondayOf(today);

        // Self-scoped, cross-gym base query (tenant filter bypassed, soft-delete re-applied). The ALL-SOURCES
        // form (no Source filter) backs the ad-hoc tracking signals; the trend narrows it to PLANNED days.
        var ownLogs = logRepository.QueryOwnAcrossGyms(currentUser.UserId).AsNoTracking();

        // D15 — ad-hoc tracking, NOT folded into the adherence %. LoggedDaysThisWeek = days in the current
        // local week (Monday-anchored, caller zone) that carry an actually-logged item (any source: an ad-hoc
        // add or a ticked planned item). HasAnyLogging = ever logged such a day. Two bounded reads.
        var loggedDaysThisWeek = await ownLogs
            .Where(l => l.LocalDate >= currentWeekStart && l.LocalDate <= today)
            .Where(NutritionMapping.HasLoggedItem)
            .CountAsync(cancellationToken);

        var hasAnyLogging = await ownLogs.AnyAsync(NutritionMapping.HasLoggedItem, cancellationToken);

        // PLANNED days only — an ad-hoc day has no plan to adhere to.
        var planned = ownLogs.Where(l => l.Source == NutritionSource.FromAssignment);

        // HasPlan reflects whether the caller has EVER had a planned nutrition day (any gym, any time), so a
        // user simply between assignments in this window still reads HasPlan=true with an empty Days list, and
        // only a never-planned user gets the empty-invite shape. One bounded EXISTS, not a full scan.
        var hasPlan = await planned.AnyAsync(cancellationToken);
        if (!hasPlan)
            return Result<NutritionAdherenceDto>.Success(
                new NutritionAdherenceDto(
                    HasPlan: false, Days: [], CurrentWeekAvgPct: null,
                    LoggedDaysThisWeek: loggedDaysThisWeek, HasAnyLogging: hasAnyLogging));

        // One bounded read of the window's planned days; counts computed in SQL via the shared projection
        // (neither the item rows nor the jsonb snapshot are loaded). The rounding rule isn't SQL-translatable,
        // so adherence is finished in memory by ToSummaryDto.
        var rows = await planned
            .Where(l => l.LocalDate >= from && l.LocalDate <= to)
            .Select(NutritionMapping.SummaryRowProjection)
            .ToListAsync(cancellationToken);

        var days = rows
            .Select(NutritionMapping.ToSummaryDto)
            .OrderBy(d => d.LocalDate)
            .Select(d => new DailyAdherenceDto(d.LocalDate, d.AdherencePct, d.PlannedCount, d.CompletedCount))
            .ToList();

        // Current local week (Monday-anchored, in the caller's zone): mean adherence over its planned days.
        var thisWeekDays = days
            .Where(d => d.LocalDate >= currentWeekStart && d.LocalDate <= today)
            .ToList();

        int? currentWeekAvgPct = thisWeekDays.Count == 0
            ? null
            : (int)Math.Round(thisWeekDays.Average(d => (double)d.AdherencePct), MidpointRounding.AwayFromZero);

        return Result<NutritionAdherenceDto>.Success(
            new NutritionAdherenceDto(
                HasPlan: true, days, currentWeekAvgPct,
                LoggedDaysThisWeek: loggedDaysThisWeek, HasAnyLogging: hasAnyLogging));
    }
}
