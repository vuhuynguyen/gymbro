using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.ExerciseModule.Entities;

public class ExerciseWarning : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public string Content { get; private set; } = null!;

    private ExerciseWarning()
    {
    }

    public ExerciseWarning(Guid exerciseId, string content)
    {
        ExerciseId = exerciseId;
        Content = content;
    }
}
