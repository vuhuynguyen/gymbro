using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.ExerciseModule.Entities;

public class ExerciseInstruction : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public int StepOrder { get; private set; }
    public string Content { get; private set; } = null!;

    private ExerciseInstruction()
    {
    }

    public ExerciseInstruction(Guid exerciseId, int stepOrder, string content)
    {
        ExerciseId = exerciseId;
        StepOrder = stepOrder;
        Content = content;
    }
}
