using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.Exercise.Entities;

public class ExerciseMedia : BaseEntity
{
    public Guid ExerciseId { get; set; }
    public string Type { get; set; } // Image | Video
    public string Url { get; set; }
    
    private ExerciseMedia() { }
    
    public ExerciseMedia(Guid exerciseId, string type, string url)
    {
        ExerciseId = exerciseId;
        Type = type;
        Url = url;
    }
}