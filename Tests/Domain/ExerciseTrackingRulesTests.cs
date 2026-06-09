using BuildingBlocks.Shared.Tracking;
using Xunit;

namespace Gymbro.Tests.Domain;

/// <summary>
/// The mode→metric matrix is the single source of truth for "which set metrics a tracking type requires".
/// These assert the primary-metric rule the LogSet handler and both clients rely on: strength needs reps,
/// cardio needs duration or distance, HIIT needs rounds or a work duration, timed needs duration, and
/// mobility/custom accept a metric-less *completed* set (mark-done).
/// </summary>
public sealed class ExerciseTrackingRulesTests
{
    private static bool Has(
        ExerciseTrackingType type,
        int? reps = null,
        decimal? weight = null,
        int? duration = null,
        int? distance = null,
        int? rounds = null,
        bool isCompleted = true) =>
        ExerciseTrackingRules.HasRequiredMetric(type, reps, weight, duration, distance, rounds, isCompleted);

    [Fact]
    public void Strength_requires_reps()
    {
        Assert.True(Has(ExerciseTrackingType.Strength, reps: 5));
        Assert.False(Has(ExerciseTrackingType.Strength, weight: 100m)); // weight alone is not enough
        Assert.False(Has(ExerciseTrackingType.Strength));
    }

    [Fact]
    public void Cardio_accepts_duration_or_distance_but_not_reps()
    {
        Assert.True(Has(ExerciseTrackingType.Cardio, duration: 600));
        Assert.True(Has(ExerciseTrackingType.Cardio, distance: 2000));
        Assert.False(Has(ExerciseTrackingType.Cardio, reps: 10));
        Assert.False(Has(ExerciseTrackingType.Cardio));
    }

    [Fact]
    public void Timed_requires_duration()
    {
        Assert.True(Has(ExerciseTrackingType.Timed, duration: 45));
        Assert.False(Has(ExerciseTrackingType.Timed, reps: 1));
    }

    [Fact]
    public void Hiit_accepts_rounds_or_duration()
    {
        Assert.True(Has(ExerciseTrackingType.Hiit, rounds: 5));
        Assert.True(Has(ExerciseTrackingType.Hiit, duration: 30));
        Assert.False(Has(ExerciseTrackingType.Hiit, reps: 10));
    }

    [Fact]
    public void Mobility_and_custom_allow_a_completion_only_set()
    {
        Assert.True(Has(ExerciseTrackingType.Mobility, isCompleted: true));
        Assert.True(Has(ExerciseTrackingType.Custom, isCompleted: true));
        // Custom with nothing present and not completed has no primary metric.
        Assert.False(Has(ExerciseTrackingType.Custom, isCompleted: false));
    }

    [Fact]
    public void Non_positive_values_do_not_count_as_present()
    {
        Assert.False(Has(ExerciseTrackingType.Strength, reps: 0));
        Assert.False(Has(ExerciseTrackingType.Cardio, duration: 0, distance: 0));
    }

    [Fact]
    public void Profile_falls_back_to_strength_for_unknown_value()
    {
        var profile = ExerciseTrackingRules.Profile((ExerciseTrackingType)999);
        Assert.Equal(ExerciseTrackingType.Strength, profile.Type);
    }
}
