using Modules.NutritionModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Queries;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Phase 1 unified personal training experience: the <c>api/me/*</c> read models aggregate the caller's
/// own data across all gyms via <c>QueryOwnAcrossGyms</c>, which deliberately bypasses the EF tenant
/// filter. These tests drive the real MediatR pipeline against the seeded fixture and assert the
/// guarantee that matters most once the filter is bypassed: a user can never see another user's data.
/// Skips automatically when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UnifiedPersonalTrainingTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task My_history_lists_only_the_callers_own_sessions()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(
            new GetMyWorkoutHistoryQuery(From: null, To: null, Status: null, Page: 1, PageSize: 50));

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientAId, s.TraineeId));
        Assert.Contains(result.Value!.Items, s => s.Id == fixture.SessionAId);
        Assert.DoesNotContain(result.Value!.Items, s => s.Id == fixture.SessionBId);
    }

    [SkippableFact]
    public async Task My_session_detail_returns_the_callers_own_session()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetMyWorkoutSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.SessionAId, result.Value!.Id);
        Assert.Equal(fixture.ClientAId, result.Value!.TraineeId);
    }

    [SkippableFact]
    public async Task My_session_detail_hides_another_users_session_as_not_found()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // Self-scoped: another user's session id simply doesn't resolve — NotFound, never a leak.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetMyWorkoutSessionByIdQuery(fixture.SessionBId));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task My_progress_and_records_resolve_for_the_caller()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var progress = await fixture.SendAsync(new GetMyProgressQuery());
        var records = await fixture.SendAsync(new GetMyPersonalRecordsQuery());

        Assert.True(progress.IsSuccess);
        Assert.True(records.IsSuccess);
    }

    [SkippableFact]
    public async Task My_progress_overview_reproduces_the_seeded_top_lift_e1rm_through_the_real_pipeline()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // M2 — validates the EF→SQL projection against real Postgres, not just translation: ClientA has 4
        // completed bench sessions, each carrying a Working set, a heavier Working set, and a Drop stage. The
        // overview's nested `Where(... Working && e1RM != null && Reps <= 12).Max(e1RM)` over `e.Sets` must run
        // server-side and surface exactly the heavier set's 132.0 — proving the drop cluster was neither counted
        // (null e1RM) nor allowed to drop the lift. Also drives the internal goal lookup + PR teaser end to end.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetMyProgressOverviewQuery());

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.NotNull(dto.ThisWeek);
        Assert.Equal(12, dto.Consistency.WindowWeeks);
        Assert.NotNull(dto.RecentPrs);
        // ClientA has no active assignment seeded → no goal, ring hidden client-side.
        Assert.Null(dto.ThisWeek.Goal);
        Assert.False(dto.ThisWeek.HasActivePlan);
        // Exactly one top lift (the seeded bench), with the MAX qualifying working-set e1RM = 132.0.
        var lift = Assert.Single(dto.TopLifts);
        Assert.Equal(fixture.BenchExerciseId, lift.ExerciseId);
        Assert.Equal(PostgresFixture.ExpectedBenchCurrentE1rmKg, lift.CurrentE1rmKg);
        // 4 sessions → 4 session-best spark points, each the per-session MAX (132.0), proving one point/session.
        Assert.Equal(4, lift.SparkE1rmKg.Count);
        Assert.All(lift.SparkE1rmKg, p => Assert.Equal(PostgresFixture.ExpectedBenchCurrentE1rmKg, p));
    }

    [SkippableFact]
    public async Task My_progress_overview_never_reads_another_users_data()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // IDOR guarantee for the bypass path, made DISCRIMINATING: ClientA and ClientB have DIFFERENT
        // completed-session counts this week (1 vs 2). The self-scoping (QueryOwnAcrossGyms(currentUser.UserId))
        // must give each caller exactly its OWN count — not the other's, and not a coincidental 0 == 0. If the
        // scope ever leaked, the counts would cross-contaminate; the distinct expected values catch that.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var asClientA = await fixture.SendAsync(new GetMyProgressOverviewQuery());

        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);
        var asClientB = await fixture.SendAsync(new GetMyProgressOverviewQuery());

        Assert.True(asClientA.IsSuccess);
        Assert.True(asClientB.IsSuccess);
        // Each caller sees only its own completed sessions: ClientA = 1 this week, ClientB = 2 this week.
        Assert.Equal(PostgresFixture.ClientACompletedThisWeek, asClientA.Value!.ThisWeek.CompletedSessions);
        Assert.Equal(PostgresFixture.ClientBCompletedThisWeek, asClientB.Value!.ThisWeek.CompletedSessions);
        Assert.NotEqual(asClientA.Value!.ThisWeek.CompletedSessions, asClientB.Value!.ThisWeek.CompletedSessions);
        // And ClientB's strength sets don't bleed into ClientA's top lifts, nor vice-versa.
        Assert.Single(asClientA.Value!.TopLifts);
        Assert.Empty(asClientB.Value!.TopLifts);
    }

    // ── Phase 2: per-lift e1RM series ──

    [SkippableFact]
    public async Task My_e1rm_series_reproduces_the_seeded_per_session_max_through_the_real_pipeline()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // The same EF→SQL honesty-gated nested Where+Max over e.Sets as the overview, but for the drill-down:
        // ClientA's 4 bench sessions each surface 132.0 (the heavier working set), one PR at the first session,
        // and the shared-calculator strength summary. Proves the projection ran in Postgres.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(
            new GetMyExerciseE1rmSeriesQuery(fixture.BenchExerciseId, From: null, To: null));

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(fixture.BenchExerciseId, dto.ExerciseId);
        Assert.Equal("Strength", dto.TrackingType);
        // 4 sessions → 4 session-best points, each the per-session MAX (132.0), top set = 120×3.
        Assert.Equal(4, dto.Points.Count);
        Assert.All(dto.Points, p => Assert.Equal(PostgresFixture.ExpectedBenchCurrentE1rmKg, p.SessionBestE1rmKg));
        Assert.All(dto.Points, p => Assert.Equal(120m, p.TopSetWeightKg));
        Assert.All(dto.Points, p => Assert.Equal(3, p.TopSetReps));
        // All sessions tie at 132.0 ⇒ only the FIRST is a PR (strictly-exceeds running max).
        Assert.Single(dto.Points, p => p.IsPr);
        Assert.True(dto.Points[0].IsPr);
        Assert.Equal(PostgresFixture.ExpectedBenchCurrentE1rmKg, dto.CurrentE1rmKg);
    }

    [SkippableFact]
    public async Task My_e1rm_series_is_empty_for_an_unknown_lift_and_never_reads_another_users_data()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // ClientB never trained the bench (only ClientA did). Asking for the bench id as ClientB must return
        // an empty series — not ClientA's 132.0 points. A random unknown id is empty for everyone too.
        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);

        var benchAsB = await fixture.SendAsync(
            new GetMyExerciseE1rmSeriesQuery(fixture.BenchExerciseId, From: null, To: null));
        var unknown = await fixture.SendAsync(
            new GetMyExerciseE1rmSeriesQuery(Guid.NewGuid(), From: null, To: null));

        Assert.True(benchAsB.IsSuccess);
        Assert.Empty(benchAsB.Value!.Points);       // ClientA's bench data never leaks to ClientB
        Assert.True(unknown.IsSuccess);
        Assert.Empty(unknown.Value!.Points);
    }

    // ── Phase 2: body-metric series ──

    [SkippableFact]
    public async Task My_metric_series_is_latest_per_day_and_case_insensitive_through_the_real_pipeline()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // ClientA logged two "weight" entries on MetricDayOld (latest = 81.0) + one on MetricDayNew. A
        // mixed-case "WEIGHT" must match (case-insensitive), and each day collapses to its latest check-in.
        // Bound the query to exactly the fixture's two seeded days so the series is deterministic regardless
        // of test order, the current date, or other tests' shared-DB weight check-ins on ClientA (e.g.
        // NutritionFlowTests seeds a "weight" entry on a different day that would otherwise drift into the
        // default 12-week window and add a phantom third point).
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetMyMetricSeriesQuery(
            "WEIGHT", From: PostgresFixture.MetricDayOld, To: PostgresFixture.MetricDayNew));

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal("weight", dto.Type);
        Assert.Equal("kg", dto.Unit);
        Assert.Equal(2, dto.Points.Count);   // two distinct days, latest-per-day
        Assert.Equal(PostgresFixture.MetricDayOld, dto.Points[0].LocalDate);
        Assert.Equal(PostgresFixture.MetricOldDayLatestKg, dto.Points[0].Value);   // the LATER old-day check-in
        Assert.Equal(PostgresFixture.MetricDayNew, dto.Points[1].LocalDate);
        Assert.Equal(PostgresFixture.MetricNewDayKg, dto.Points[1].Value);
    }

    [SkippableFact]
    public async Task My_metric_series_never_reads_another_users_data()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // ClientA has a weight series; ClientB logged none. Self-scoping must give B an EMPTY series, never
        // A's points — a discriminating IDOR check (A non-empty vs B empty), not 0 == 0.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var asA = await fixture.SendAsync(new GetMyMetricSeriesQuery("weight", From: null, To: null));

        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);
        var asB = await fixture.SendAsync(new GetMyMetricSeriesQuery("weight", From: null, To: null));

        Assert.True(asA.IsSuccess);
        Assert.True(asB.IsSuccess);
        Assert.NotEmpty(asA.Value!.Points);    // ClientA has data
        Assert.Empty(asB.Value!.Points);       // ClientB's series is its own (empty) — A's never leaks
    }
}
