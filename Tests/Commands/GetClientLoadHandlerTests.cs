using System.Reflection;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Queries.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The coach per-client acute-vs-chronic LOAD (api/clients/{id}/progress/load, Phase 4), fully mocked — no
/// database. Pins the frozen rules from API-CONTRACTS §4.3 + FEASIBILITY R2/R10: it is a SEPARATE,
/// TENANT-SCOPED handler that reads the tenant-filtered <c>Query()</c> and NEVER <c>QueryOwnAcrossGyms</c>; it
/// gates on <c>WorkoutLogViewAll</c> via ResourceAccessGuard (a plain member is forbidden) and requires the
/// trainee to be a member of the active tenant (a non-member → 404, never a silent rescope to self); it exposes
/// the acute (7-day) and chronic (28-day ÷ 4 weekly-average) volumes SEPARATELY with a soft trend band — and
/// NEVER an ACWR ratio (no such field exists on the DTO). Volume parity with SessionMapping.ComputeVolumeKg
/// (Σ weight×reps over Working sets, both values present). Time-relative to UtcNow.
/// </summary>
public sealed class GetClientLoadHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Coach = Guid.NewGuid();

    // ── handler wiring ──

    private static GetClientLoadHandler CreateSut(
        IEnumerable<WorkoutSession> sessions,
        bool coachHasViewAll = true,
        bool traineeIsMember = true,
        Action<IWorkoutSessionRepository>? trackRepo = null)
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.Query().Returns(new TestAsyncEnumerable<WorkoutSession>(sessions));
        repo.QueryOwnAcrossGyms(Arg.Any<Guid>())
            .Returns(new TestAsyncEnumerable<WorkoutSession>(Array.Empty<WorkoutSession>()));
        trackRepo?.Invoke(repo);

        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        tenantAuth.CanAccessResourceAsync(
                Tenant, Permission.WorkoutLogViewOwn, Permission.WorkoutLogViewAll,
                Arg.Any<Guid>(), Tenant, Arg.Any<CancellationToken>())
            .Returns(coachHasViewAll);

        var roleResolver = Substitute.For<ITenantRoleResolver>();
        roleResolver.GetRoleAsync(Arg.Any<Guid>(), Tenant, Arg.Any<CancellationToken>())
            .Returns(traineeIsMember ? TenantRole.Client : (TenantRole?)null);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Tenant);

        return new GetClientLoadHandler(repo, tenantAuth, roleResolver, tenantContext);
    }

    private static Task<Result<AcuteChronicLoadDto>> Run(
        Guid traineeId,
        IEnumerable<WorkoutSession> sessions,
        bool coachHasViewAll = true,
        bool traineeIsMember = true)
        => CreateSut(sessions, coachHasViewAll, traineeIsMember)
            .Handle(new GetClientLoadQuery(traineeId), CancellationToken.None);

    // ── entity seeding ──

    // A completed session `daysAgo` before now, with one working set of `reps`×`weightKg` (volume = reps×weight).
    private static WorkoutSession LiftSession(
        Guid traineeId, int daysAgo, int reps, decimal weightKg, params PerformedSet[] extraSets)
    {
        var session = WorkoutSession.Start(
            traineeId, Tenant, SessionSource.Adhoc, null, null, "Lift", null, "UTC", null);
        SetProp(session, "StartedAt", DateTimeOffset.UtcNow.AddDays(-daysAgo));
        SetProp(session, "Status", SessionStatus.Completed);

        var sets = new List<PerformedSet> { WorkingSet(reps, weightKg) };
        sets.AddRange(extraSets);
        Backing<PerformedExercise>(session, "_exercises")
            .Add(Exercise(Guid.NewGuid(), "Bench", ExerciseTrackingType.Strength, sets.ToArray()));
        return session;
    }

    private static PerformedExercise Exercise(
        Guid exerciseId, string name, ExerciseTrackingType trackingType, params PerformedSet[] sets)
    {
        var exercise = PerformedExercise.Create(Guid.NewGuid(), Tenant, exerciseId, null, 0, name, trackingType);
        Backing<PerformedSet>(exercise, "_sets").AddRange(sets);
        return exercise;
    }

    private static PerformedSet WorkingSet(int reps, decimal weightKg)
        => PerformedSet.Log(Guid.NewGuid(), Tenant, null, 1, PerformedSetType.Working,
            reps, weightKg, null, null, null, null, true);

    private static PerformedSet WarmupSet(int reps, decimal weightKg)
        => PerformedSet.Log(Guid.NewGuid(), Tenant, null, 9, PerformedSetType.Warmup,
            reps, weightKg, null, null, null, null, true);

    private static void SetProp(object target, string name, object value)
        => target.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(target, value);

    private static List<T> Backing<T>(object target, string field)
        => (List<T>)target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(target)!;

    // ── auth ──

    [Fact]
    public async Task Coach_without_view_all_is_forbidden()
    {
        var client = Guid.NewGuid();
        var result = await Run(client, new[] { LiftSession(client, 1, 5, 100m) }, coachHasViewAll: false);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Non_member_trainee_is_not_found_never_rescoped_to_self()
    {
        var nonMember = Guid.NewGuid();
        // The coach HAS ViewAll, but the requested id is not a member of the active tenant → 404.
        var result = await Run(nonMember, new[] { LiftSession(nonMember, 1, 5, 100m) }, traineeIsMember: false);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Missing_tenant_header_is_a_validation_failure()
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var roleResolver = Substitute.For<ITenantRoleResolver>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns((Guid?)null);

        var sut = new GetClientLoadHandler(repo, tenantAuth, roleResolver, tenantContext);
        var result = await sut.Handle(new GetClientLoadQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BuildingBlocks.Shared.Errors.ErrorType.Validation, result.Error.Type);
    }

    // ── tenant-scoping (R2) ──

    [Fact]
    public async Task Reads_tenant_filtered_query_and_never_the_cross_gym_path()
    {
        var client = Guid.NewGuid();

        IWorkoutSessionRepository? captured = null;
        var sut = CreateSut(new[] { LiftSession(client, 1, 5, 100m) }, trackRepo: r => captured = r);
        var result = await sut.Handle(new GetClientLoadQuery(client), CancellationToken.None);

        Assert.True(result.IsSuccess);
        captured!.Received().Query();
        captured!.DidNotReceive().QueryOwnAcrossGyms(Arg.Any<Guid>());
    }

    // ── acute / chronic volume from the seeded graph ──

    [Fact]
    public async Task Acute_is_last_7_days_chronic_is_28_day_total_over_four_weeks()
    {
        var client = Guid.NewGuid();
        // One 1000 kg session in each of the four 7-day windows (days 1, 8, 15, 22). All four are inside the
        // 28-day chronic window; only day-1 is inside the 7-day acute window.
        var sessions = new[]
        {
            LiftSession(client, 1, 10, 100m),   // 1000 kg — acute + chronic
            LiftSession(client, 8, 10, 100m),   // 1000 kg — chronic only
            LiftSession(client, 15, 10, 100m),  // 1000 kg — chronic only
            LiftSession(client, 22, 10, 100m),  // 1000 kg — chronic only
        };

        var result = await Run(client, sessions);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(1000m, dto.AcuteVolumeKg);          // only the day-1 session
        Assert.Equal(1000m, dto.ChronicWeeklyVolumeKg);  // 4000 kg ÷ 4 weeks = 1000 kg/week
    }

    [Fact]
    public async Task Sessions_older_than_28_days_are_excluded_from_both_windows()
    {
        var client = Guid.NewGuid();
        var sessions = new[]
        {
            LiftSession(client, 3, 10, 100m),    // 1000 kg — acute + chronic
            LiftSession(client, 40, 10, 100m),   // 1000 kg — OUTSIDE the chronic window, must be ignored
        };

        var result = await Run(client, sessions);

        var dto = result.Value!;
        Assert.Equal(1000m, dto.AcuteVolumeKg);
        Assert.Equal(250m, dto.ChronicWeeklyVolumeKg);   // only the day-3 1000 kg counts ⇒ 1000 ÷ 4
    }

    [Fact]
    public async Task Volume_parity_only_counts_working_sets_with_weight_and_reps()
    {
        var client = Guid.NewGuid();
        // Working 5×100 = 500 kg counts; the warmup must NOT (parity with SessionMapping.ComputeVolumeKg).
        var sessions = new[] { LiftSession(client, 1, 5, 100m, WarmupSet(20, 200m)) };

        var result = await Run(client, sessions);

        var dto = result.Value!;
        Assert.Equal(500m, dto.AcuteVolumeKg);           // warmup 4000 kg excluded
        Assert.Equal(125m, dto.ChronicWeeklyVolumeKg);   // 500 ÷ 4
    }

    // ── 200 + zeros when no sessions in window ──

    [Fact]
    public async Task No_sessions_in_window_returns_zeros_and_steady_never_204()
    {
        var client = Guid.NewGuid();
        var result = await Run(client, Array.Empty<WorkoutSession>());

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(0m, dto.AcuteVolumeKg);
        Assert.Equal(0m, dto.ChronicWeeklyVolumeKg);
        Assert.Equal(LoadTrend.Steady, dto.Trend);
    }

    // ── trend band at its thresholds (a SOFT nudge — no ratio is exposed) ──

    [Fact]
    public async Task Trend_is_ramping_when_acute_far_exceeds_chronic_weekly_average()
    {
        var client = Guid.NewGuid();
        // Chronic baseline weeks (days 9, 16, 23): 500 kg each ⇒ chronic total 1500, weekly avg 375. Acute
        // (day 1): a heavy 2000 kg ⇒ 2000 > 1.5 × 375 (562.5) ⇒ Ramping.
        var sessions = new[]
        {
            LiftSession(client, 1, 20, 100m),   // 2000 kg acute (+chronic)
            LiftSession(client, 9, 5, 100m),    // 500
            LiftSession(client, 16, 5, 100m),   // 500
            LiftSession(client, 23, 5, 100m),   // 500
        };

        var result = await Run(client, sessions);
        Assert.Equal(LoadTrend.Ramping, result.Value!.Trend);
    }

    [Fact]
    public async Task Trend_is_detraining_when_acute_falls_well_below_chronic_weekly_average()
    {
        var client = Guid.NewGuid();
        // Chronic baseline weeks (days 9, 16, 23): 1000 kg each ⇒ weekly avg = (3000+small acute) ÷ 4. Acute
        // (day 1) a light 100 kg ⇒ well under 0.8 × weekly avg ⇒ Detraining.
        var sessions = new[]
        {
            LiftSession(client, 1, 1, 100m),    // 100 kg acute
            LiftSession(client, 9, 10, 100m),   // 1000
            LiftSession(client, 16, 10, 100m),  // 1000
            LiftSession(client, 23, 10, 100m),  // 1000
        };

        var result = await Run(client, sessions);

        var dto = result.Value!;
        // chronic total = 3100 ⇒ weekly 775; acute 100 < 0.8 × 775 (620) ⇒ Detraining.
        Assert.Equal(775m, dto.ChronicWeeklyVolumeKg);
        Assert.Equal(100m, dto.AcuteVolumeKg);
        Assert.Equal(LoadTrend.Detraining, dto.Trend);
    }

    [Fact]
    public async Task Trend_is_steady_when_acute_tracks_the_chronic_weekly_average()
    {
        var client = Guid.NewGuid();
        // Each of the four 7-day windows carries 1000 kg ⇒ chronic weekly avg 1000; acute 1000 ⇒ ratio 1.0,
        // inside [0.8, 1.5] ⇒ Steady. (The band, not a published ratio, decides.)
        var sessions = new[]
        {
            LiftSession(client, 1, 10, 100m),
            LiftSession(client, 8, 10, 100m),
            LiftSession(client, 15, 10, 100m),
            LiftSession(client, 22, 10, 100m),
        };

        var result = await Run(client, sessions);

        var dto = result.Value!;
        Assert.Equal(1000m, dto.AcuteVolumeKg);
        Assert.Equal(1000m, dto.ChronicWeeklyVolumeKg);
        Assert.Equal(LoadTrend.Steady, dto.Trend);
    }

    [Fact]
    public async Task A_first_week_from_no_chronic_baseline_is_ramping_not_a_divide_by_zero()
    {
        var client = Guid.NewGuid();
        // Acute work but no prior chronic baseline (only a day-1 session) ⇒ chronicWeekly = 250 here, so this
        // also guards the genuinely-empty-baseline branch: a brand-new client's first session never divides by
        // zero. Acute 1000 > 1.5 × 250 ⇒ Ramping.
        var result = await Run(client, new[] { LiftSession(client, 1, 10, 100m) });

        var dto = result.Value!;
        Assert.Equal(1000m, dto.AcuteVolumeKg);
        Assert.Equal(250m, dto.ChronicWeeklyVolumeKg);
        Assert.Equal(LoadTrend.Ramping, dto.Trend);
    }
}
