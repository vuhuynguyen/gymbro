using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.Exercise.Entities;

public class ExerciseTag : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public string Tag { get; private set; }
    
    private ExerciseTag() { }
    
    public ExerciseTag(Guid exerciseId, string tag)
    {
        ExerciseId = exerciseId;
        Tag = tag;
    }
}