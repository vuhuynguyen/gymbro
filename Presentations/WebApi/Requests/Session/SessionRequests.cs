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
    public int SetNumber { get; set; }
    public PerformedSetType SetType { get; set; } = PerformedSetType.Working;
    public int? Reps { get; set; }
    public decimal? WeightKg { get; set; }
    public int? DurationSeconds { get; set; }
    public int? DistanceM { get; set; }
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
    public int? Rpe { get; set; }
    public int? RestSeconds { get; set; }
    public bool? IsCompleted { get; set; }
    public PerformedSetType? SetType { get; set; }
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
