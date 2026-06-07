using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.ExerciseModule.Entities;

public class ExerciseMedia : BaseEntity
{
    public Guid ExerciseId { get; set; }
    public string Type { get; set; } = null!; // Image | Video
    public string Url { get; set; } = null!;
    
    private ExerciseMedia() { }
    
    public ExerciseMedia(Guid exerciseId, string type, string url)
    {
        ExerciseId = exerciseId;
        Type = type;
        Url = url;
    }
}