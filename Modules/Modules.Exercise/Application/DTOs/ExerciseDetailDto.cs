namespace Modules.ExerciseModule.Application.DTOs;

public class ExerciseDetailDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Type { get; set; }
    public required string TrackingType { get; set; }
    public required string MovementType { get; set; }
    public required string Difficulty { get; set; }
    public required string Equipment { get; set; }
    public int? EstimatedCaloriesBurn { get; set; }
    public int? AverageDurationSeconds { get; set; }
    public required string MuscleGroup { get; set; }

    /// <summary>Fine-grained library category (one of the 13 codes, e.g. <c>biceps</c>/<c>glutes</c>/<c>cardio</c>).</summary>
    public string? Category { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Comma-separated specific (fine) muscle slugs for the activation map — primary then secondary.</summary>
    public string? DetailedPrimaryMuscles { get; set; }
    public string? DetailedSecondaryMuscles { get; set; }

    public List<ExerciseMuscleItemDto> Muscles { get; set; } = [];

    public List<string> Instructions { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<ExerciseMediaItemDto> Media { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class ExerciseMediaItemDto
{
    public required string Url { get; set; }
    public required string Type { get; set; }
}

public class ExerciseMuscleItemDto
{
    public required string Muscle { get; set; }
    public bool IsPrimary { get; set; }
}
