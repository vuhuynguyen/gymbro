using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.Exercise.Entities;

/// <summary>
/// Exercise bounded context. Related rows use ExerciseId foreign keys only (no reference-typed navigations from children to this aggregate).
/// Other feature modules must not reference this project; integrate via shared MediatR contracts (e.g. in the application layer).
/// </summary>
public class Exercise : AggregateRoot, ISharedEntity, ISoftDelete
{
    public string DefaultName { get; private set; }
    public string DefaultDescription { get; private set; }
    public MuscleGroup MuscleGroup { get; private set; }
    public string ImageUrl { get; private set; }

    private readonly List<ExerciseInstruction> _instructions = new();
    public IReadOnlyCollection<ExerciseInstruction> Instructions => _instructions;

    private readonly List<ExerciseTag> _tags = new();
    public IReadOnlyCollection<ExerciseTag> Tags => _tags;
    
    private readonly List<ExerciseMedia> _media = new();
    public IReadOnlyCollection<ExerciseMedia> Media => _media;
    
    private readonly List<ExerciseWarning> _warnings = new();
    public IReadOnlyCollection<ExerciseWarning> Warnings => _warnings;
    
    private Exercise() { }
    
    public static Exercise CreateGlobal(
        string name,
        MuscleGroup muscleGroup,
        string imageUrl,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required");

        return new Exercise
        {
            TenantId = null,
            DefaultName = name,
            MuscleGroup = muscleGroup,
            ImageUrl = imageUrl,
            DefaultDescription = description
        };
    }
    
    public void UpdateInfo(string name, string description)
    {
        DefaultName = name;
        DefaultDescription = description;
    }
    
    public void AddInstruction(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        var stepOrder = _instructions.Count + 1;

        _instructions.Add(new ExerciseInstruction(Id, stepOrder, content));
    }
    
    public void AddTag(string tag)
    {
        if (_tags.Any(x => x.Tag == tag))
            return;

        _tags.Add(new ExerciseTag(Id, tag));
    }
    
    public void AddMedia(string url, string type)
    {
        _media.Add(new ExerciseMedia(Id, url, type));
    }
    
    public void AddWarning(string content)
    {
        _warnings.Add(new ExerciseWarning(Id, content));
    }
}