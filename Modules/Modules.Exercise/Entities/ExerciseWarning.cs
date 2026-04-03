using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.Exercise.Entities;

public class ExerciseWarning : BaseEntity
{
    public Guid ExerciseId { get; set; }
    public string Content { get; set; }
    
    private ExerciseWarning() { }
    
    public ExerciseWarning(Guid exerciseId, string content)
    {
        ExerciseId = exerciseId;
        Content = content;
    }
}