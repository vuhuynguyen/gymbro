using Modules.WorkoutSessionModule.Entities;
using BuildingBlocks.Shared.DomainPrimitives;
using Xunit;

namespace Gymbro.Tests.Domain;

public sealed class PerformedSetTests
{
    private static PerformedSet LogWorking(int reps, decimal weightKg) =>
        PerformedSet.Log(
            performedExerciseId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            planSetId: null,
            setNumber: 1,
            setType: PerformedSetType.Working,
            reps: reps,
            weightKg: weightKg,
            durationSeconds: null,
            distanceM: null,
            rpe: null,
            restSeconds: null,
            isCompleted: true);

    // ── e1RM calculation (Epley formula: weight × (1 + reps / 30), rounded to 1 dp) ──

    [Theory]
    [InlineData(10, 100, 133.3)]  // 100 × (1 + 10/30) = 133.3̄ → 133.3
    [InlineData(1,  100, 103.3)]  // 100 × (1 + 1/30)  = 103.3̄ → 103.3
    [InlineData(30, 100, 200.0)]  // 100 × (1 + 30/30) = 200.0
    [InlineData(5,  120, 140.0)]  // 120 × (1 + 5/30)  = 140.0
    public void Working_set_computes_e1rm(int reps, decimal weight, decimal expected)
    {
        var set = LogWorking(reps, weight);
        Assert.Equal(expected, set.EstimatedOneRepMaxKg);
    }

    [Theory]
    [InlineData(PerformedSetType.Warmup)]
    [InlineData(PerformedSetType.Drop)]
    [InlineData(PerformedSetType.Amrap)]
    [InlineData(PerformedSetType.Failure)]
    public void Non_working_set_types_produce_null_e1rm(PerformedSetType setType)
    {
        var set = PerformedSet.Log(
            Guid.NewGuid(), Guid.NewGuid(), null, 1, setType,
            10, 100m, null, null, null, null, true);

        Assert.Null(set.EstimatedOneRepMaxKg);
    }

    [Fact]
    public void Zero_reps_produces_null_e1rm()
    {
        var set = PerformedSet.Log(
            Guid.NewGuid(), Guid.NewGuid(), null, 1, PerformedSetType.Working,
            0, 100m, null, null, null, null, true);

        Assert.Null(set.EstimatedOneRepMaxKg);
    }

    [Fact]
    public void Null_weight_produces_null_e1rm()
    {
        var set = PerformedSet.Log(
            Guid.NewGuid(), Guid.NewGuid(), null, 1, PerformedSetType.Working,
            10, null, null, null, null, null, true);

        Assert.Null(set.EstimatedOneRepMaxKg);
    }

    [Fact]
    public void Log_throws_when_performedExerciseId_is_empty()
    {
        Assert.Throws<DomainException>(() =>
            PerformedSet.Log(Guid.Empty, Guid.NewGuid(), null, 1, PerformedSetType.Working,
                10, 100m, null, null, null, null, true));
    }

    [Fact]
    public void Log_throws_when_tenantId_is_empty()
    {
        Assert.Throws<DomainException>(() =>
            PerformedSet.Log(Guid.NewGuid(), Guid.Empty, null, 1, PerformedSetType.Working,
                10, 100m, null, null, null, null, true));
    }

    // ── Edit recalculates e1RM ─────────────────────────────────────────────

    [Fact]
    public void Edit_recalculates_e1rm_when_reps_or_weight_change()
    {
        var set = LogWorking(10, 100m);  // initial e1RM = 133.3

        set.Edit(reps: 5, weightKg: 120m, null, null, null, null, null, null);

        Assert.Equal(140.0m, set.EstimatedOneRepMaxKg);
    }

    [Fact]
    public void Edit_to_non_working_type_clears_e1rm()
    {
        var set = LogWorking(10, 100m);

        set.Edit(null, null, null, null, null, null, null, PerformedSetType.Warmup);

        Assert.Null(set.EstimatedOneRepMaxKg);
    }

    [Fact]
    public void Edit_from_non_working_to_working_computes_e1rm()
    {
        var set = PerformedSet.Log(
            Guid.NewGuid(), Guid.NewGuid(), null, 1, PerformedSetType.Warmup,
            10, 100m, null, null, null, null, true);

        Assert.Null(set.EstimatedOneRepMaxKg);

        set.Edit(null, null, null, null, null, null, null, PerformedSetType.Working);

        Assert.Equal(133.3m, set.EstimatedOneRepMaxKg);
    }

    [Fact]
    public void Edit_updates_only_provided_fields()
    {
        var set = LogWorking(10, 100m);

        set.Edit(reps: 12, weightKg: null, null, null, rpe: 8, null, null, null);

        Assert.Equal(12, set.Reps);
        Assert.Equal(100m, set.WeightKg);  // unchanged
        Assert.Equal(8, set.Rpe);
    }
}
