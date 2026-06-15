using System.Reflection;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.UserModule.Entities;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// The R2 cross-gym isolation guarantee for the COACH surface (api/clients/*), driven through the real MediatR
/// pipeline + EF global filters against Postgres. This is the single most dangerous item in the Progress
/// redesign (COACH-VS-TRAINEE.md §4): the trainee per-lift series deliberately turns the tenant filter OFF
/// (QueryOwnAcrossGyms), so if the coach roster/strength endpoints reused that path keyed by a traineeId, they
/// would leak a client's sessions from EVERY gym. These facts seed a DEDICATED client who trains in gym A AND
/// gym B (kept separate from the fixture's ClientA so the shared seed is not polluted), then assert a coach in
/// gym A sees ONLY gym-A work — the gym-B PR must be absent from both the roster counts and the e1RM series —
/// and that a non-member trainee is rejected, never silently rescoped to self.
///
/// Skips automatically when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CoachClientProgressIsolationTests(PostgresFixture fixture)
{
    // A dedicated cross-gym client, stable for the fixture's lifetime so re-seeding is idempotent. The lift
    // ids are assigned by the catalog factory at seed time and captured here (set once).
    private static readonly Guid CrossGymClientId = Guid.Parse("c0a55ec0-0000-4000-8000-000000000a01");
    private static Guid _gymABenchId;
    private static Guid _gymBDeadliftId;

    // Gym A (in-gym) bench: 120×3 ⇒ e1RM 132.0. Gym B (rival) deadlift: 160×5 ⇒ a clearly heavier e1RM that,
    // if it leaked, would dominate the series and inflate the roster — so its absence is provable.
    private const decimal GymABenchE1rmKg = 132.0m;

    // Load (Phase 4) parity: each gym-A bench is 120×3 = 360 kg of working-set volume; four of them over the
    // 28-day chronic window ⇒ 1440 kg total ⇒ 360 kg/week. The gym-B deadlift is 160×5 = 800 kg — if the tenant
    // filter leaked, the chronic weekly average would read 560 kg, not 360.
    private const decimal GymABenchVolumePerSessionKg = 360.0m;
    private const decimal GymAChronicWeeklyVolumeKg = 360.0m;   // (4 × 360) ÷ 4 weeks
    private const decimal GymBDeadliftVolumeKg = 800.0m;

    /// <summary>Cross-gym graph: a dedicated client trains in BOTH the Iron gym (4 weekly bench sessions, one
    /// this week) and the Rival gym (a heavier deadlift this week). Idempotent — the integration fixture is
    /// shared across the collection, so re-seeding looks up and reuses an already-seeded graph.</summary>
    private async Task EnsureCrossGymClientSeededAsync()
    {
        await fixture.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();

            // Admin so the inserts/lookups bypass the tenant/soft-delete filters.
            fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);

            if (await db.Set<User>().AnyAsync(u => u.Id == CrossGymClientId))
            {
                // Already seeded by a prior test in this collection — recover the captured lift ids.
                _gymABenchId = await LiftIdByNameAsync(db, "XGym Bench");
                _gymBDeadliftId = await LiftIdByNameAsync(db, "XGym Deadlift");
                return;
            }

            db.Set<User>().Add(User.Create(CrossGymClientId, "Cyrus CrossGym"));
            // Member of BOTH gyms — the cross-gym membership that makes a leak possible.
            db.Set<UserTenantRole>().AddRange(
                UserTenantRole.Create(CrossGymClientId, fixture.TenantId, TenantRole.Client),
                UserTenantRole.Create(CrossGymClientId, fixture.OtherTenantId, TenantRole.Client));

            // Distinct catalog exercises; the factory assigns ids — capture them so the leak is unambiguous.
            var bench = CatalogLift("XGym Bench", Modules.ExerciseModule.Entities.MuscleGroup.Chest);
            var deadlift = CatalogLift("XGym Deadlift", Modules.ExerciseModule.Entities.MuscleGroup.Back);
            _gymABenchId = bench.Id;
            _gymBDeadliftId = deadlift.Id;
            db.Set<Modules.ExerciseModule.Entities.Exercise>().AddRange(bench, deadlift);

            // Gym A: 4 bench sessions, each a 120×3 working set (e1RM 132.0). Dated relative to `now` (NOT a
            // Monday anchor) so the test is deterministic on any weekday: exactly ONE lands today — in both the
            // rolling 7-day acute window (load) and the current week (roster) — while the other three sit a clear
            // week+ apart, outside the 7-day window but inside the 28-day chronic / 12-week strength windows (so
            // the ≥4-session top-lift bar still clears). A Monday-anchored seed was a time-bomb: on some weekdays
            // the rolling 7-day acute window spanned two consecutive Mondays and double-counted (acute 720 ≠ 360).
            var now = DateTimeOffset.UtcNow;
            var benchDates = new[] { now.AddMinutes(-30), now.AddDays(-9), now.AddDays(-16), now.AddDays(-23) };
            foreach (var startedAt in benchDates)
                db.Set<WorkoutSession>().Add(StrengthSession(
                    fixture.TenantId, startedAt, _gymABenchId, "XGym Bench", reps: 3, weightKg: 120m));

            // Gym B (rival): a single heavier deadlift this week — the cross-gym work that must stay invisible.
            db.Set<WorkoutSession>().Add(StrengthSession(
                fixture.OtherTenantId, DateTimeOffset.UtcNow.AddDays(-1), _gymBDeadliftId, "XGym Deadlift",
                reps: 5, weightKg: 160m));

            await db.SaveChangesAsync();
        });
    }

    [SkippableFact]
    public async Task Coach_roster_counts_only_in_gym_sessions_for_a_cross_gym_client()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        await EnsureCrossGymClientSeededAsync();

        // The gym-A coach reads the roster for gym A (Iron Gym).
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new GetClientRosterQuery());

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value!.Items, i => i.TraineeId == CrossGymClientId);

        // Exactly ONE gym-A completed session this week (the seeded week-0 bench). The gym-B session this week
        // must NOT inflate this count — if the tenant filter leaked, it would read 2.
        Assert.Equal(1, row.CompletedThisWeek);
        Assert.NotNull(row.LastActiveAt);
    }

    [SkippableFact]
    public async Task Coach_client_strength_excludes_gym_b_work_for_a_cross_gym_client()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        await EnsureCrossGymClientSeededAsync();

        // The gym-A coach opens the client's strength detail (in gym A).
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new GetClientStrengthQuery(CrossGymClientId, Take: 6));

        Assert.True(result.IsSuccess);
        var trends = result.Value!;

        // The gym-B deadlift (heavier) must be ABSENT — the coach sees only gym-A lifts.
        Assert.DoesNotContain(trends, t => t.ExerciseId == _gymBDeadliftId);

        // The only top lift is the gym-A bench, and its e1RM is the IN-GYM 132.0 — never the heavier gym-B work.
        var bench = Assert.Single(trends);
        Assert.Equal(_gymABenchId, bench.ExerciseId);
        Assert.Equal(GymABenchE1rmKg, bench.CurrentE1rmKg);
        // Every spark point is the in-gym 132.0; not one carries the rival gym's heavier e1RM.
        Assert.Equal(4, bench.SparkE1rmKg.Count);
        Assert.All(bench.SparkE1rmKg, p => Assert.Equal(GymABenchE1rmKg, p));
    }

    [SkippableFact]
    public async Task Coach_client_load_excludes_gym_b_volume_for_a_cross_gym_client()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        await EnsureCrossGymClientSeededAsync();

        // The gym-A coach opens the client's acute-vs-chronic load (in gym A).
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new GetClientLoadQuery(CrossGymClientId));

        Assert.True(result.IsSuccess);
        var load = result.Value!;

        // Chronic weekly average is the IN-GYM 360 kg/week (4 × 360 ÷ 4) — never 560 kg/week, which is what a
        // leak of the gym-B 800 kg deadlift would produce. The rival-gym volume is invisible by the tenant filter.
        Assert.Equal(GymAChronicWeeklyVolumeKg, load.ChronicWeeklyVolumeKg);
        Assert.NotEqual((4 * GymABenchVolumePerSessionKg + GymBDeadliftVolumeKg) / 4, load.ChronicWeeklyVolumeKg);

        // The 7-day acute window is bounded by gym-A work only (≤ one bench session this week); it can never
        // carry the gym-B 800 kg deadlift, which would push acute above a single in-gym bench's 360 kg.
        Assert.True(load.AcuteVolumeKg <= GymABenchVolumePerSessionKg);
    }

    [SkippableFact]
    public async Task Cross_gym_client_self_view_DOES_see_both_gyms_proving_the_isolation_is_coach_only()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        await EnsureCrossGymClientSeededAsync();

        // The SAME client, self-scoped (QueryOwnAcrossGyms), DOES see the rival-gym deadlift — proving the
        // gym-B data really exists and that the coach view's exclusion is the tenant filter at work, not a
        // missing seed. (Self-scoped is cross-gym by design; the coach view is the opposite.)
        fixture.Principal.Become(CrossGymClientId, fixture.TenantId);
        var rival = await fixture.SendAsync(
            new GetMyExerciseE1rmSeriesQuery(_gymBDeadliftId, From: null, To: null));

        Assert.True(rival.IsSuccess);
        Assert.NotEmpty(rival.Value!.Points);   // the trainee sees their gym-B work; the coach (above) did not
    }

    [SkippableFact]
    public async Task Coach_in_gym_a_cannot_read_a_client_who_is_not_a_member_of_gym_a()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // OtherOwner is a member of the RIVAL gym only, not gym A. The gym-A coach asking for their strength
        // must get 404 — the trainee is not a member of the active tenant (never a silent rescope to self).
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new GetClientStrengthQuery(fixture.OtherOwnerId, Take: 6));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        // Same 404 boundary for the load endpoint — a non-member id is never silently rescoped to self.
        var load = await fixture.SendAsync(new GetClientLoadQuery(fixture.OtherOwnerId));
        Assert.True(load.IsFailure);
        Assert.Equal("NotFound", load.Error.Code);
    }

    [SkippableFact]
    public async Task A_plain_member_cannot_read_the_roster_or_another_clients_strength()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // ClientB is a plain Client in gym A (WorkoutLogViewOwn only) — no roster, no other client's strength.
        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);

        var roster = await fixture.SendAsync(new GetClientRosterQuery());
        Assert.True(roster.IsFailure);
        Assert.Equal("Forbidden", roster.Error.Code);

        var strength = await fixture.SendAsync(new GetClientStrengthQuery(fixture.ClientAId, Take: 6));
        Assert.True(strength.IsFailure);
        Assert.Equal("Unauthorized", strength.Error.Code);

        var load = await fixture.SendAsync(new GetClientLoadQuery(fixture.ClientAId));
        Assert.True(load.IsFailure);
        Assert.Equal("Unauthorized", load.Error.Code);
    }

    // ── seeding helpers (private setters + UtcNow stamps ⇒ reflection) ──

    private static Modules.ExerciseModule.Entities.Exercise CatalogLift(
        string name, Modules.ExerciseModule.Entities.MuscleGroup muscle)
        => Modules.ExerciseModule.Entities.Exercise.CreateGlobal(
            name, "", "",
            Modules.ExerciseModule.Entities.ExerciseType.Strength,
            Modules.ExerciseModule.Entities.MovementType.Compound,
            Modules.ExerciseModule.Entities.DifficultyLevel.Intermediate,
            Modules.ExerciseModule.Entities.Equipment.Barbell, null, null,
            new[] { (muscle, true) }, ExerciseTrackingType.Strength);

    private static Task<Guid> LiftIdByNameAsync(AppDbContext db, string name)
        => db.Set<Modules.ExerciseModule.Entities.Exercise>()
            .Where(e => e.DefaultName == name)
            .Select(e => e.Id)
            .FirstAsync();

    private static WorkoutSession StrengthSession(
        Guid tenantId, DateTimeOffset startedAt, Guid exerciseId, string exerciseName, int reps, decimal weightKg)
    {
        var session = WorkoutSession.Start(
            CrossGymClientId, tenantId, SessionSource.Adhoc, null, null, "XGym", null, "UTC", null);
        SetProp(session, "StartedAt", startedAt);
        SetProp(session, "Status", SessionStatus.Completed);

        var exercise = PerformedExercise.Create(
            session.Id, tenantId, exerciseId, null, 0, exerciseName, ExerciseTrackingType.Strength);
        var set = PerformedSet.Log(exercise.Id, tenantId, null, 1, PerformedSetType.Working,
            reps, weightKg, null, null, null, null, true);
        Backing<PerformedSet>(exercise, "_sets").Add(set);
        Backing<PerformedExercise>(session, "_exercises").Add(exercise);
        return session;
    }

    private static DateOnly MondayOfUtcWeek(DateTimeOffset instant)
    {
        var date = DateOnly.FromDateTime(instant.UtcDateTime);
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }

    private static void SetProp(object target, string name, object value)
        => target.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(target, value);

    private static List<T> Backing<T>(object target, string field)
        => (List<T>)target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(target)!;
}
