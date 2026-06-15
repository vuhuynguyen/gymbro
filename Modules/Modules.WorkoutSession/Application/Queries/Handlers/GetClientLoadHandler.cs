using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// One client's acute-vs-chronic training LOAD for the coach detail view (api/clients/{id}/progress/load,
/// Phase 4), TENANT-SCOPED to the active gym.
///
/// <para>R2 isolation (the single most dangerous item): this is a SEPARATE handler from the self-scoped trainee
/// path. It reads through <see cref="IWorkoutSessionRepository.Query"/> with the EF tenant filter ON — so a
/// client who trains in multiple gyms contributes ONLY their in-gym volume. It NEVER calls
/// <c>QueryOwnAcrossGyms</c> and is NEVER parameterized by the trainee id over the self-scoped path. The exact
/// caller gate + membership check mirror <see cref="GetClientStrengthHandler"/>: <c>WorkoutLogViewAll</c> +
/// <see cref="ResourceAccessGuard"/> bind the coach to their own gym, and <see cref="ITenantRoleResolver"/>
/// confirms the trainee is a member of the active tenant — a non-member yields a failure (404 / 403), never a
/// silent rescope to self.</para>
///
/// <para>The signal is VOLUME-BASED only (FEASIBILITY R10 — RpeOverall is integer-only and frequently null,
/// DurationSeconds is wall-clock incl. rest): <c>AcuteVolumeKg</c> = Σ working-set volume over the last 7 days;
/// <c>ChronicWeeklyVolumeKg</c> = (Σ working-set volume over the last 28 days) ÷ 4 = the average weekly load.
/// Per-set volume uses the SAME predicate as <c>SessionMapping.ComputeVolumeKg</c> (Σ weight×reps over
/// <c>Working</c> sets carrying both values — drop/AMRAP stages count, so <c>ParentSetId</c> is NOT filtered,
/// keeping parity with the trainee progress volume). The two raw values are exposed SEPARATELY and the trend
/// is a SOFT band (Ramping/Steady/Detraining) — we NEVER compute or expose an ACWR ratio. Returns zeros + a
/// Steady trend (200, never 204) when there are no in-gym sessions in the window.</para>
/// </summary>
public sealed class GetClientLoadHandler(
    IWorkoutSessionRepository sessionRepository,
    ITenantAuthorizationService tenantAuth,
    ITenantRoleResolver roleResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetClientLoadQuery, Result<AcuteChronicLoadDto>>
{
    private const int AcuteDays = 7;
    private const int ChronicDays = 28;
    private const int ChronicWeeks = ChronicDays / 7;   // 4 — chronic Σ ÷ 4 = average weekly load

    // SOFT trend band — a gentle nudge, never a medical/injury claim. Compares acute to the chronic weekly
    // average; we keep the comparison internal and expose only the two raw volumes (NEVER the ratio — R10).
    private const decimal RampingFactor = 1.5m;    // acute > ~1.5× chronic weekly avg ⇒ Ramping
    private const decimal DetrainingFactor = 0.8m; // acute < ~0.8× chronic weekly avg ⇒ Detraining

    public async Task<Result<AcuteChronicLoadDto>> Handle(
        GetClientLoadQuery request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
            return Result<AcuteChronicLoadDto>.Failure(
                Validation("TenantId", "X-Tenant-Id header is required."));

        // Coach gate (caller side): only a WorkoutLogViewAll caller bound to THIS gym may read another client;
        // a plain member is denied (CanAccessResourceAsync would only ever let them read their OWN id).
        if (!await ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync(
                tenantAuth, tenantId, request.TraineeId, tenantId, cancellationToken))
            return Result<AcuteChronicLoadDto>.Failure(
                Unauthorized("Unauthorized", "You cannot view this client's training load."));

        // Membership row-level check (subject side): the requested trainee must ALSO be a member of the active
        // tenant. A non-member id resolves to no role → 404 (cross-tenant invisibility), never a rescope.
        if (await roleResolver.GetRoleAsync(request.TraineeId, tenantId, cancellationToken) is null)
            return Result<AcuteChronicLoadDto>.Failure(
                NotFound("NotFound", "This client is not a member of the gym."));

        var now = DateTimeOffset.UtcNow;
        var acuteFrom = now.AddDays(-AcuteDays);
        var chronicFrom = now.AddDays(-ChronicDays);

        // TENANT-FILTERED read (filter ON): the client's completed sessions in THIS gym over the 28-day chronic
        // window, each with its working-set volume projected in SQL. The acute 7-day slice is taken in memory
        // from the same rows. Per-set volume = Σ weight×reps over Working sets carrying both values — identical
        // to SessionMapping.ComputeVolumeKg / GetMyProgressHandler (ParentSetId NOT filtered ⇒ stages count).
        var rows = await sessionRepository.Query()
            .Where(s => s.Status == SessionStatus.Completed
                && s.TraineeId == request.TraineeId
                && s.StartedAt >= chronicFrom)
            .Select(s => new
            {
                s.StartedAt,
                VolumeKg = s.Exercises
                    .SelectMany(e => e.Sets)
                    .Where(set => set.SetType == PerformedSetType.Working
                        && set.WeightKg != null && set.Reps != null)
                    .Sum(set => (decimal?)(set.WeightKg!.Value * set.Reps!.Value)) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var chronicTotal = rows.Sum(r => r.VolumeKg);
        var acuteVolume = rows.Where(r => r.StartedAt >= acuteFrom).Sum(r => r.VolumeKg);
        var chronicWeekly = chronicTotal / ChronicWeeks;

        var trend = ClassifyTrend(acuteVolume, chronicWeekly);

        return Result<AcuteChronicLoadDto>.Success(
            new AcuteChronicLoadDto(acuteVolume, chronicWeekly, trend));
    }

    // SOFT band over acute vs the chronic WEEKLY average. Internal-only — the response carries the two raw
    // volumes, NEVER this ratio (R10: an exposed ACWR reads as a clinical injury claim on RPE-free data).
    private static LoadTrend ClassifyTrend(decimal acuteVolume, decimal chronicWeekly)
    {
        // No chronic baseline yet: a first week of work is "ramping up from nothing"; truly empty stays Steady.
        if (chronicWeekly <= 0m)
            return acuteVolume > 0m ? LoadTrend.Ramping : LoadTrend.Steady;

        if (acuteVolume > chronicWeekly * RampingFactor)
            return LoadTrend.Ramping;

        if (acuteVolume < chronicWeekly * DetrainingFactor)
            return LoadTrend.Detraining;

        return LoadTrend.Steady;
    }
}
