namespace Modules.ExerciseModule.Application.DTOs;

public class ExerciseDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string TrackingType { get; set; }
    public required string MovementType { get; set; }
    public required string Difficulty { get; set; }
    public required string Equipment { get; set; }
    public int? EstimatedCaloriesBurn { get; set; }
    public int? AverageDurationSeconds { get; set; }
    public required string MuscleGroup { get; set; }
    public string? ImageUrl { get; set; }

    /// All targeted muscles (primary + secondary), so clients can show full involvement — not just the
    /// single primary group. Ordered primary-first.
    public List<ExerciseMuscleItemDto> Muscles { get; set; } = [];
}
