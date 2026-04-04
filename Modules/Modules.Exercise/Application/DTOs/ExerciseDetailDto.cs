namespace Modules.ExerciseModule.Application.DTOs;

public class ExerciseDetailDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string MuscleGroup { get; set; }

    public List<string> Instructions { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> MediaUrls { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}