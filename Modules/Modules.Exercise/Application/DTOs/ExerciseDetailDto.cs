namespace Modules.ExerciseModule.Application.DTOs;

public class ExerciseDetailDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Type { get; set; }
    public required string MovementType { get; set; }
    public required string Difficulty { get; set; }
    public required string Equipment { get; set; }
    public int? EstimatedCaloriesBurn { get; set; }
    public int? AverageDurationSeconds { get; set; }
    public required string MuscleGroup { get; set; }
    public string? ImageUrl { get; set; }

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
