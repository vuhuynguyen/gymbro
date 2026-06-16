namespace BuildingBlocks.Shared.Tracking;

/// <summary>The measurable metrics a logged/prescribed set can carry. Used as a flag set on <see cref="TrackingProfile"/>.</summary>
[Flags]
public enum TrackingMetric
{
    None = 0,
    Reps = 1 << 0,
    Weight = 1 << 1,
    Duration = 1 << 2,
    Distance = 1 << 3,
    Rounds = 1 << 4,
    Rest = 1 << 5,
    Rpe = 1 << 6,
    Calories = 1 << 7,
    HeartRate = 1 << 8,
    /// <summary>Treadmill/ramp grade as a percentage — cardio intensity that distance/time don't capture.</summary>
    Incline = 1 << 9,
    /// <summary>Pace in km/h — treadmill/bike speed setting.</summary>
    Speed = 1 << 10,
    /// <summary>Machine resistance/level (bike/elliptical/stair) — a unitless intensity knob.</summary>
    Level = 1 << 11
}

/// <summary>
/// Declares, for one <see cref="ExerciseTrackingType"/>, which metrics are relevant.
/// <paramref name="Primary"/> = at least one must be present to log a set (unless <paramref name="AllowCompletionOnly"/>);
/// <paramref name="Allowed"/> = the metrics the UI shows and the API accepts as meaningful.
/// </summary>
public sealed record TrackingProfile(
    ExerciseTrackingType Type,
    TrackingMetric Primary,
    TrackingMetric Allowed,
    bool AllowCompletionOnly);

/// <summary>
/// Single source of truth for "which metrics apply to which tracking type". Replaces the strength-only assumptions
/// that were hardcoded in the plan builder and the session loggers. The frontends mirror this matrix to shape inputs.
/// </summary>
public static class ExerciseTrackingRules
{
    private const TrackingMetric AllMeasurable =
        TrackingMetric.Reps | TrackingMetric.Weight | TrackingMetric.Duration |
        TrackingMetric.Distance | TrackingMetric.Rounds;

    private static readonly IReadOnlyDictionary<ExerciseTrackingType, TrackingProfile> Profiles =
        new Dictionary<ExerciseTrackingType, TrackingProfile>
        {
            [ExerciseTrackingType.Strength] = new(
                ExerciseTrackingType.Strength,
                Primary: TrackingMetric.Reps,
                Allowed: TrackingMetric.Reps | TrackingMetric.Weight | TrackingMetric.Rpe | TrackingMetric.Rest,
                AllowCompletionOnly: false),

            [ExerciseTrackingType.Bodyweight] = new(
                ExerciseTrackingType.Bodyweight,
                Primary: TrackingMetric.Reps,
                Allowed: TrackingMetric.Reps | TrackingMetric.Weight | TrackingMetric.Duration | TrackingMetric.Rpe | TrackingMetric.Rest,
                AllowCompletionOnly: false),

            [ExerciseTrackingType.Cardio] = new(
                ExerciseTrackingType.Cardio,
                Primary: TrackingMetric.Duration | TrackingMetric.Distance,
                // Incline/Speed/Level are optional intensity metrics (treadmill grade, pace, machine
                // resistance) — never required, but accepted and shown for cardio machines.
                Allowed: TrackingMetric.Duration | TrackingMetric.Distance | TrackingMetric.Calories | TrackingMetric.HeartRate | TrackingMetric.Rpe | TrackingMetric.Incline | TrackingMetric.Speed | TrackingMetric.Level,
                AllowCompletionOnly: false),

            [ExerciseTrackingType.Timed] = new(
                ExerciseTrackingType.Timed,
                Primary: TrackingMetric.Duration,
                // Weight is optional load for weighted holds (weighted plank/wall-sit/dead-hang); the
                // duration is still what's required.
                Allowed: TrackingMetric.Duration | TrackingMetric.Weight | TrackingMetric.Rpe | TrackingMetric.Rest,
                AllowCompletionOnly: false),

            [ExerciseTrackingType.Hiit] = new(
                ExerciseTrackingType.Hiit,
                Primary: TrackingMetric.Rounds | TrackingMetric.Duration,
                Allowed: TrackingMetric.Rounds | TrackingMetric.Duration | TrackingMetric.Rest | TrackingMetric.Calories | TrackingMetric.HeartRate | TrackingMetric.Rpe,
                AllowCompletionOnly: false),

            [ExerciseTrackingType.Mobility] = new(
                ExerciseTrackingType.Mobility,
                Primary: TrackingMetric.None,
                Allowed: TrackingMetric.Duration | TrackingMetric.Reps | TrackingMetric.Rest,
                AllowCompletionOnly: true),

            [ExerciseTrackingType.Custom] = new(
                ExerciseTrackingType.Custom,
                Primary: AllMeasurable,
                Allowed: AllMeasurable | TrackingMetric.Rest | TrackingMetric.Rpe | TrackingMetric.Calories | TrackingMetric.HeartRate,
                AllowCompletionOnly: true),
        };

    /// <summary>The metric profile for a tracking type (falls back to Strength for unknown values).</summary>
    public static TrackingProfile Profile(ExerciseTrackingType type) =>
        Profiles.TryGetValue(type, out var p) ? p : Profiles[ExerciseTrackingType.Strength];

    /// <summary>
    /// True when a logged set carries at least the primary metric for its tracking type. A value counts as present
    /// only when positive (the field validators already reject non-positive values). Completion-only modes accept a
    /// metric-less set that is marked completed.
    /// </summary>
    public static bool HasRequiredMetric(
        ExerciseTrackingType type,
        int? reps,
        decimal? weightKg,
        int? durationSeconds,
        int? distanceM,
        int? rounds,
        bool isCompleted)
    {
        var profile = Profile(type);

        if (profile.AllowCompletionOnly && isCompleted)
            return true;
        if (profile.Primary == TrackingMetric.None)
            return true;

        var present = PresentMetrics(reps, weightKg, durationSeconds, distanceM, rounds);
        return (present & profile.Primary) != TrackingMetric.None;
    }

    /// <summary>Human-readable hint describing what a set of this tracking type needs, for validation/UI messages.</summary>
    public static string RequiredMetricMessage(ExerciseTrackingType type) => type switch
    {
        ExerciseTrackingType.Strength or ExerciseTrackingType.Bodyweight => "Enter reps to log this set.",
        ExerciseTrackingType.Cardio => "Enter a duration or distance to log this set.",
        ExerciseTrackingType.Timed => "Enter a duration to log this set.",
        ExerciseTrackingType.Hiit => "Enter rounds or a work duration to log this set.",
        ExerciseTrackingType.Mobility => "Mark the set completed or enter a duration.",
        _ => "Enter at least one metric to log this set."
    };

    private static TrackingMetric PresentMetrics(int? reps, decimal? weightKg, int? durationSeconds, int? distanceM, int? rounds)
    {
        var present = TrackingMetric.None;
        if (reps is > 0) present |= TrackingMetric.Reps;
        if (weightKg is > 0) present |= TrackingMetric.Weight;
        if (durationSeconds is > 0) present |= TrackingMetric.Duration;
        if (distanceM is > 0) present |= TrackingMetric.Distance;
        if (rounds is > 0) present |= TrackingMetric.Rounds;
        return present;
    }
}
