using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Entities;

namespace WebApi.Requests.Session;

public sealed class StartSessionRequest
{
    public SessionSource Source { get; set; } = SessionSource.Adhoc;
    public Guid? PlanAssignmentId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public string? ClientTimezone { get; set; }
    public decimal? BodyweightKg { get; set; }
}

public sealed class AddExerciseRequest
{
    public Guid ExerciseId { get; set; }
    public Guid? PlanWorkoutExerciseId { get; set; }
    public int Order { get; set; }
    public string? Notes { get; set; }

    /// <summary>Optional: group this ad-hoc exercise into a superset with others sharing the same id.</summary>
    public Guid? SupersetGroupId { get; set; }
}

public sealed class UpdateExerciseRequest
{
    public ExerciseUpdateAction Action { get; set; }
    public Guid? SubstituteExerciseId { get; set; }
    public string? Notes { get; set; }
}

public sealed class LogSetRequest
{
    public Guid? PlanSetId { get; set; }
    /// <summary>Set when this is a drop/rest-pause stage of an existing lead set (the cluster counts as one set).</summary>
    public Guid? ParentSetId { get; set; }
    public int SetNumber { get; set; }
    public PerformedSetType SetType { get; set; } = PerformedSetType.Working;
    public int? Reps { get; set; }
    public decimal? WeightKg { get; set; }
    public int? DurationSeconds { get; set; }
    public int? DistanceM { get; set; }
    public int? Calories { get; set; }
    public int? AvgHeartRate { get; set; }
    public int? Rounds { get; set; }
    /// <summary>Treadmill/ramp incline %, optional cardio intensity.</summary>
    public decimal? InclinePercent { get; set; }
    /// <summary>Pace in km/h, optional treadmill/bike speed.</summary>
    public decimal? SpeedKph { get; set; }
    /// <summary>Machine resistance/level (bike/elliptical/stair), optional.</summary>
    public int? Level { get; set; }
    public int? Rpe { get; set; }
    public int? RestSeconds { get; set; }
    public bool IsCompleted { get; set; } = true;
}

public sealed class EditSetRequest
{
    public int? Reps { get; set; }
    public decimal? WeightKg { get; set; }
    public int? DurationSeconds { get; set; }
    public int? DistanceM { get; set; }
    public int? Calories { get; set; }
    public int? AvgHeartRate { get; set; }
    public int? Rounds { get; set; }
    public decimal? InclinePercent { get; set; }
    public decimal? SpeedKph { get; set; }
    public int? Level { get; set; }
    public int? Rpe { get; set; }
    public int? RestSeconds { get; set; }
    public bool? IsCompleted { get; set; }
    public PerformedSetType? SetType { get; set; }
}

public sealed class ReorderSetsRequest
{
    /// The exercise's set ids in their new order (must be exactly the exercise's current sets).
    public List<Guid> SetIds { get; set; } = [];
}

public sealed class CompleteSessionRequest
{
    public int? RpeOverall { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AbandonSessionRequest
{
    public string? Notes { get; set; }
}
