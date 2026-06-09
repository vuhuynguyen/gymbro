namespace WebApi.Requests.WorkoutPlan;

using Modules.WorkoutPlanModule.Application;
using Modules.WorkoutPlanModule.Entities;

public sealed class CreateWorkoutPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationWeeks { get; set; }
    public int? WorkoutsPerWeek { get; set; }
}

public sealed class UpdateWorkoutPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationWeeks { get; set; }
    public int? WorkoutsPerWeek { get; set; }
}

public sealed class ReplaceWorkoutPlanStructureRequest
{
    // Metadata is sent together with the structure so a plan-builder save lands as ONE new version.
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationWeeks { get; set; }
    public int? WorkoutsPerWeek { get; set; }
    public List<PlanWorkoutStructureRequest> Workouts { get; set; } = new();
}

public sealed class PlanWorkoutStructureRequest
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<PlanWorkoutExerciseRequest> Exercises { get; set; } = new();
}

public sealed class PlanWorkoutExerciseRequest
{
    public Guid ExerciseId { get; set; }
    public int Order { get; set; }
    public List<PlanSetRequest> Sets { get; set; } = new();

    /// <summary>Exercises sharing a non-null group id in a workout are prescribed as a superset.</summary>
    public Guid? SupersetGroupId { get; set; }
}

public sealed class PlanSetRequest
{
    public PlanSetType SetType { get; set; } = PlanSetType.Working;
    public int? TargetReps { get; set; }
    public decimal? TargetWeightKg { get; set; }
    public int? TargetRpe { get; set; }
    public int? TargetDurationSeconds { get; set; }
    public int? TargetDistanceM { get; set; }
    public int? TargetRounds { get; set; }
    public int RestSeconds { get; set; }
    public int Order { get; set; }
}

public sealed class CreatePlanAssignmentRequest
{
    public Guid TraineeId { get; set; }
    public Guid PlanId { get; set; }
    public DateOnly StartDate { get; set; }
    public int FrequencyDaysPerWeek { get; set; }
    public PlanVisibilityMode VisibilityMode { get; set; } = PlanVisibilityMode.Guided;
    public bool HideExercises { get; set; }
    public bool HideSetsReps { get; set; }
    public bool HideFutureWorkouts { get; set; }
    public bool DisableTraineeEditing { get; set; }
    public string? SnapshotJson { get; set; }
}

public sealed class UpdatePlanAssignmentToLatestVersionRequest
{
    public string? SnapshotJson { get; set; }
}

public sealed class UpdatePlanAssignmentRequest
{
    public DateOnly? StartDate { get; set; }
    public int FrequencyDaysPerWeek { get; set; }
    public PlanVisibilityMode VisibilityMode { get; set; } = PlanVisibilityMode.Guided;
    public bool HideExercises { get; set; }
    public bool HideSetsReps { get; set; }
    public bool HideFutureWorkouts { get; set; }
    public bool DisableTraineeEditing { get; set; }
}
