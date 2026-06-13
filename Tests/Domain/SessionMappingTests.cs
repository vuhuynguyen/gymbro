using System.Reflection;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Domain;

/// <summary>
/// Pure session-analytics math: working-set volume, 1-based plan-week bucketing, and estimated-1RM PR
/// detection. These back the headline "you hit a PR", per-session volume and weekly-progress numbers and
/// previously had no direct coverage — a refactor that dropped the working-set filter, double-excluded drop
/// stages from volume, or relaxed the strictly-greater PR rule would have shipped green.
/// </summary>
public sealed class SessionMappingTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // PerformedExercise owns its sets in a private collection that EF populates (there is no public Add —
    // sets are written through the repository). For a pure aggregate test we seed the backing field directly.
    private static PerformedExercise ExerciseWith(Guid exerciseId, params PerformedSet[] sets)
    {
        var exercise = PerformedExercise.Create(Guid.NewGuid(), Tenant, exerciseId, null, 0, "Lift");
        var backing = (List<PerformedSet>)typeof(PerformedExercise)
            .GetField("_sets", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(exercise)!;
        backing.AddRange(sets);
        return exercise;
    }

    private static PerformedSet Set(PerformedSetType type, int? reps, decimal? weight, Guid? parent = null)
        => PerformedSet.Log(
            performedExerciseId: Guid.NewGuid(), tenantId: Tenant, planSetId: null, setNumber: 1,
            setType: type, reps: reps, weightKg: weight, durationSeconds: null, distanceM: null,
            rpe: null, restSeconds: null, isCompleted: true, parentSetId: parent);

    private static PerformedSet Working(int reps, decimal weight, Guid? parent = null)
        => Set(PerformedSetType.Working, reps, weight, parent);

    // ── ComputeVolumeKg: Σ(weight × reps) over working sets carrying both values ──

    [Fact]
    public void Volume_sums_weight_times_reps_over_working_sets_with_both_values()
    {
        var exercise = ExerciseWith(Guid.NewGuid(),
            Working(5, 100m),                            // 500
            Working(3, 50m),                             // 150
            Set(PerformedSetType.Warmup, 10, 40m),       // excluded: not a working set
            Set(PerformedSetType.Working, 5, null),      // excluded: no weight
            Set(PerformedSetType.Working, null, 80m));   // excluded: no reps

        Assert.Equal(650m, SessionMapping.ComputeVolumeKg(new[] { exercise }));
    }

    [Fact]
    public void Volume_counts_drop_set_stages_even_though_the_cluster_is_one_logical_set()
    {
        var lead = Working(5, 100m);                     // 500
        var stage = Working(8, 60m, parent: lead.Id);    // 480 — a stage, still volume-bearing
        var exercise = ExerciseWith(Guid.NewGuid(), lead, stage);

        // ParentSetId never excludes a row from volume (only from logical set-counts, which live in handlers).
        Assert.Equal(980m, SessionMapping.ComputeVolumeKg(new[] { exercise }));
    }

    [Fact]
    public void Volume_is_zero_when_there_are_no_qualifying_working_sets()
    {
        var exercise = ExerciseWith(Guid.NewGuid(), Set(PerformedSetType.Warmup, 10, 40m));
        Assert.Equal(0m, SessionMapping.ComputeVolumeKg(new[] { exercise }));
    }

    // ── ComputePlanWeek: 1-based week from assignment start to session date ──

    [Fact]
    public void Plan_week_is_null_without_a_start_date()
        => Assert.Null(SessionMapping.ComputePlanWeek(null, DateTimeOffset.UnixEpoch, null));

    [Fact]
    public void Plan_week_is_null_for_a_session_before_the_assignment_start()
    {
        var start = new DateOnly(2026, 1, 10);
        var dayBefore = new DateTimeOffset(start.AddDays(-1).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        Assert.Null(SessionMapping.ComputePlanWeek(start, dayBefore, null));
    }

    [Theory]
    [InlineData(0, 1)]    // assignment start day → week 1
    [InlineData(6, 1)]    // last day of week 1
    [InlineData(7, 2)]    // first day of week 2
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    public void Plan_week_buckets_days_into_one_based_weeks(int dayOffset, int expectedWeek)
    {
        var start = new DateOnly(2026, 1, 1);
        var session = new DateTimeOffset(start.AddDays(dayOffset).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        Assert.Equal(expectedWeek, SessionMapping.ComputePlanWeek(start, session, null));
    }

    [Fact]
    public void Plan_week_buckets_on_the_trainees_local_date_not_utc()
    {
        var start = new DateOnly(2026, 1, 1);
        // 2026-01-08 02:00 UTC is still 2026-01-07 (21:00) in Toronto (UTC-5): day 6 → week 1 locally,
        // but day 7 → week 2 in UTC. The trainee's captured zone must decide the bucket.
        var instant = new DateTimeOffset(2026, 1, 8, 2, 0, 0, TimeSpan.Zero);

        Assert.Equal(2, SessionMapping.ComputePlanWeek(start, instant, null));               // UTC fallback → week 2
        Assert.Equal(1, SessionMapping.ComputePlanWeek(start, instant, "America/Toronto"));  // trainee-local → week 1
    }

    // ── DetectPrs: top working set per lift whose e1RM strictly exceeds the prior best ──

    [Fact]
    public void First_ever_top_set_is_a_pr_when_there_is_no_prior_best()
    {
        var exerciseId = Guid.NewGuid();
        var top = Working(10, 100m);                       // e1RM 133.3
        var exercise = ExerciseWith(exerciseId, Working(5, 60m), top);   // 70.0 and 133.3

        var (prSetIds, prs) = SessionMapping.DetectPrs(
            new[] { exercise },
            priorBestByExercise: new Dictionary<Guid, decimal>(),
            nameById: new Dictionary<Guid, string> { [exerciseId] = "Squat" });

        Assert.Contains(top.Id, prSetIds);
        var pr = Assert.Single(prs);
        Assert.Equal(exerciseId, pr.ExerciseId);
        Assert.Equal("Squat", pr.ExerciseName);
        Assert.Equal(133.3m, pr.EstimatedOneRepMaxKg);
        Assert.Null(pr.PreviousEstimatedOneRepMaxKg);
    }

    [Fact]
    public void Top_set_strictly_above_prior_best_is_a_pr()
    {
        var exerciseId = Guid.NewGuid();
        var top = Working(10, 100m);                       // 133.3
        var exercise = ExerciseWith(exerciseId, top);

        var (prSetIds, prs) = SessionMapping.DetectPrs(
            new[] { exercise },
            new Dictionary<Guid, decimal> { [exerciseId] = 130m },
            new Dictionary<Guid, string>());

        Assert.Contains(top.Id, prSetIds);
        Assert.Equal(130m, Assert.Single(prs).PreviousEstimatedOneRepMaxKg);
    }

    [Fact]
    public void Top_set_equal_to_prior_best_is_not_a_pr()   // the rule is STRICTLY greater
    {
        var exerciseId = Guid.NewGuid();
        var exercise = ExerciseWith(exerciseId, Working(10, 100m));   // 133.3

        var (prSetIds, prs) = SessionMapping.DetectPrs(
            new[] { exercise },
            new Dictionary<Guid, decimal> { [exerciseId] = 133.3m },
            new Dictionary<Guid, string>());

        Assert.Empty(prSetIds);
        Assert.Empty(prs);
    }

    [Fact]
    public void Pr_is_the_highest_e1rm_working_set_in_the_exercise()
    {
        var exerciseId = Guid.NewGuid();
        var heavier = Working(5, 120m);    // 140.0  ← highest e1RM
        var lighter = Working(10, 100m);   // 133.3
        var exercise = ExerciseWith(exerciseId, lighter, heavier);   // deliberately out of order

        var (prSetIds, _) = SessionMapping.DetectPrs(
            new[] { exercise }, new Dictionary<Guid, decimal>(), new Dictionary<Guid, string>());

        Assert.Contains(heavier.Id, prSetIds);
        Assert.DoesNotContain(lighter.Id, prSetIds);
    }

    [Fact]
    public void Non_working_sets_never_produce_a_pr()
    {
        var exerciseId = Guid.NewGuid();
        var exercise = ExerciseWith(exerciseId, Set(PerformedSetType.Warmup, 3, 200m));   // no e1RM

        var (prSetIds, prs) = SessionMapping.DetectPrs(
            new[] { exercise }, new Dictionary<Guid, decimal>(), new Dictionary<Guid, string>());

        Assert.Empty(prSetIds);
        Assert.Empty(prs);
    }

    [Fact]
    public void Prs_are_ordered_by_e1rm_gain_descending()
    {
        var bigGain = Guid.NewGuid();     // brand-new lift (prior 0) → gain 140.0
        var smallGain = Guid.NewGuid();   // prior 130 → gain 3.3
        var ex1 = ExerciseWith(bigGain, Working(5, 120m));      // 140.0
        var ex2 = ExerciseWith(smallGain, Working(10, 100m));   // 133.3

        var (_, prs) = SessionMapping.DetectPrs(
            new[] { ex2, ex1 },   // input order is opposite to the expected output
            new Dictionary<Guid, decimal> { [smallGain] = 130m },
            new Dictionary<Guid, string>());

        Assert.Equal(2, prs.Count);
        Assert.Equal(bigGain, prs[0].ExerciseId);    // larger gain ranks first
        Assert.Equal(smallGain, prs[1].ExerciseId);
    }
}
