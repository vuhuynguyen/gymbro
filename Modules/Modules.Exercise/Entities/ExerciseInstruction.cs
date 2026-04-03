using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.Exercise.Entities;

public class ExerciseInstruction : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public int StepOrder { get; private set; }
    public string Content { get; private set; }
    
    private ExerciseInstruction() { }
    
    public ExerciseInstruction(Guid exerciseId, int stepOrder, string content)
    {
        ExerciseId = exerciseId;
        StepOrder = stepOrder;
        Content = content;
    }
}