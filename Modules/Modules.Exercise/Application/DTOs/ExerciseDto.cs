namespace Modules.ExerciseModule.Application.DTOs;

public class ExerciseDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string MuscleGroup { get; set; }
    public string? ImageUrl { get; set; }
}