using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.ExerciseModule.Entities;

public class ExerciseMuscle : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public MuscleGroup Muscle { get; private set; }
    public bool IsPrimary { get; private set; }

    private ExerciseMuscle()
    {
    }

    public ExerciseMuscle(Guid exerciseId, MuscleGroup muscle, bool isPrimary)
    {
        ExerciseId = exerciseId;
        Muscle = muscle;
        IsPrimary = isPrimary;
    }
}
