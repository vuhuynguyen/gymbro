namespace WebApi.Requests.Exercise;

public class ExerciseMuscleRequest
{
    public string Muscle { get; set; } = null!;
    public bool IsPrimary { get; set; }
}

public class ExerciseMediaItemRequest
{
    public string Url { get; set; } = null!;
    public string Type { get; set; } = "Image";
}

public class CreateExerciseRequest
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = null!;
    public string MovementType { get; set; } = null!;
    public string Difficulty { get; set; } = null!;
    public string Equipment { get; set; } = null!;

    /// <summary>Optional logging mode (Strength/Bodyweight/Cardio/Timed/Hiit/Mobility/Custom). Absent → derived from Type/Equipment.</summary>
    public string? TrackingType { get; set; }

    public int? EstimatedCaloriesBurn { get; set; }
    public int? AverageDurationSeconds { get; set; }
    public string? ImageUrl { get; set; }

    public List<string>? Instructions { get; set; }
    public List<string>? Tags { get; set; }
    public List<ExerciseMediaItemRequest>? Media { get; set; }
    public List<string>? Warnings { get; set; }

    /// <summary>
    /// Preferred: explicit muscle list. If empty, <see cref="MuscleGroup"/> is mapped to a single primary muscle.
    /// </summary>
    public List<ExerciseMuscleRequest>? Muscles { get; set; }

    /// <summary>Legacy single primary muscle when <see cref="Muscles"/> is null or empty.</summary>
    public string? MuscleGroup { get; set; }
}

public class UpdateExerciseRequest : CreateExerciseRequest;
