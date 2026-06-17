using BuildingBlocks.Shared.DomainPrimitives;
using BuildingBlocks.Shared.Tracking;

namespace Modules.ExerciseModule.Entities;

/// <summary>
/// Exercise bounded context. Related rows use ExerciseId foreign keys only (no reference-typed navigations from children to this aggregate).
/// Other feature modules must not reference this project; integrate via shared MediatR contracts (e.g. in the application layer).
/// </summary>
public class Exercise : AggregateRoot, ISharedEntity, ISoftDelete
{
    public string DefaultName { get; private set; } = null!;
    public string DefaultDescription { get; private set; } = null!;
    
    public ExerciseType Type { get; private set; }
    /// <summary>How this exercise is logged (drives which metrics a set requires/shows). Defaults to Strength.</summary>
    public ExerciseTrackingType TrackingType { get; private set; } = ExerciseTrackingType.Strength;
    public MovementType MovementType { get; private set; }
    public DifficultyLevel Difficulty { get; private set; }
    public Equipment Equipment { get; private set; }
    public int? EstimatedCaloriesBurn { get; private set; }
    public int? AverageDurationSeconds { get; private set; }
    
    public string ImageUrl { get; private set; } = null!;

    /// <summary>Comma-separated specific (fine) muscle slugs for the activation map — primary (e.g. <c>hamstring</c>).
    /// The catalog's coarse <see cref="Muscles"/> only knows 6 groups; this carries the real per-muscle detail.</summary>
    public string? DetailedPrimaryMuscles { get; private set; }
    /// <summary>Comma-separated specific (fine) muscle slugs — secondary.</summary>
    public string? DetailedSecondaryMuscles { get; private set; }
    
    private readonly List<ExerciseMuscle> _muscles = new();
    public IReadOnlyCollection<ExerciseMuscle> Muscles => _muscles;

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
        string imageUrl,
        string description,
        ExerciseType type,
        MovementType movementType,
        DifficultyLevel difficulty,
        Equipment equipment,
        int? estimatedCaloriesBurn,
        int? averageDurationSeconds,
        IReadOnlyCollection<(MuscleGroup muscle, bool isPrimary)> muscles,
        ExerciseTrackingType trackingType = ExerciseTrackingType.Strength)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required");

        ArgumentNullException.ThrowIfNull(muscles);
        if (muscles.Count == 0)
            throw new DomainException("At least one muscle is required.");
        if (!muscles.Any(m => m.isPrimary))
            throw new DomainException("At least one primary muscle is required.");
        if (muscles.Select(m => m.muscle).Distinct().Count() != muscles.Count)
            throw new DomainException("Each muscle group may appear only once.");

        if (estimatedCaloriesBurn is < 0)
            throw new DomainException("estimatedCaloriesBurn is out of range.");
        if (averageDurationSeconds is < 0)
            throw new DomainException("averageDurationSeconds is out of range.");

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            DefaultName = name.Trim(),
            ImageUrl = imageUrl ?? string.Empty,
            DefaultDescription = description ?? string.Empty,
            Type = type,
            TrackingType = trackingType,
            MovementType = movementType,
            Difficulty = difficulty,
            Equipment = equipment,
            EstimatedCaloriesBurn = estimatedCaloriesBurn,
            AverageDurationSeconds = averageDurationSeconds
        };

        foreach (var (muscle, isPrimary) in muscles)
            exercise.AddMuscle(muscle, isPrimary);

        return exercise;
    }
    
    public void AddMuscle(MuscleGroup muscle, bool isPrimary)
    {
        if (_muscles.Any(m => m.Muscle == muscle))
            return;

        _muscles.Add(new ExerciseMuscle(Id, muscle, isPrimary));
    }
    
    public void UpdateInfo(string name, string description)
    {
        DefaultName = name;
        DefaultDescription = description;
    }

    public void UpdateCatalog(
        string name,
        string description,
        string imageUrl,
        ExerciseType type,
        MovementType movementType,
        DifficultyLevel difficulty,
        Equipment equipment,
        int? estimatedCaloriesBurn,
        int? averageDurationSeconds,
        ExerciseTrackingType trackingType = ExerciseTrackingType.Strength)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required");

        if (estimatedCaloriesBurn is < 0)
            throw new DomainException("estimatedCaloriesBurn is out of range.");
        if (averageDurationSeconds is < 0)
            throw new DomainException("averageDurationSeconds is out of range.");

        DefaultName = name.Trim();
        DefaultDescription = description ?? string.Empty;
        ImageUrl = imageUrl ?? string.Empty;
        Type = type;
        TrackingType = trackingType;
        MovementType = movementType;
        Difficulty = difficulty;
        Equipment = equipment;
        EstimatedCaloriesBurn = estimatedCaloriesBurn;
        AverageDurationSeconds = averageDurationSeconds;
    }

    public void ReplaceMuscles(IReadOnlyCollection<(MuscleGroup muscle, bool isPrimary)> muscles)
    {
        ArgumentNullException.ThrowIfNull(muscles);
        if (muscles.Count == 0)
            throw new DomainException("At least one muscle is required.");
        if (!muscles.Any(m => m.isPrimary))
            throw new DomainException("At least one primary muscle is required.");
        if (muscles.Select(m => m.muscle).Distinct().Count() != muscles.Count)
            throw new DomainException("Each muscle group may appear only once.");

        _muscles.Clear();
        foreach (var (muscle, isPrimary) in muscles)
            _muscles.Add(new ExerciseMuscle(Id, muscle, isPrimary));
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
        _media.Add(new ExerciseMedia(Id, type, url));
    }

    /// <summary>Sets the specific (fine) muscle slugs that drive the activation map (comma-separated, nullable).</summary>
    public void SetDetailedMuscles(string? primary, string? secondary)
    {
        DetailedPrimaryMuscles = string.IsNullOrWhiteSpace(primary) ? null : primary.Trim();
        DetailedSecondaryMuscles = string.IsNullOrWhiteSpace(secondary) ? null : secondary.Trim();
    }
    
    public void AddWarning(string content)
    {
        _warnings.Add(new ExerciseWarning(Id, content));
    }

    /// <summary>Replaces ordered instruction steps (non-empty lines only; order preserved).</summary>
    public void ReplaceInstructions(IReadOnlyList<string> orderedSteps)
    {
        ArgumentNullException.ThrowIfNull(orderedSteps);
        _instructions.Clear();
        var order = 1;
        foreach (var raw in orderedSteps)
        {
            var content = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                continue;

            _instructions.Add(new ExerciseInstruction(Id, order++, content));
        }
    }

    /// <summary>Replaces tags (trimmed, case-insensitive distinct).</summary>
    public void ReplaceTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags.Clear();
        foreach (var t in tags
                     .Select(x => x?.Trim() ?? string.Empty)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _tags.Add(new ExerciseTag(Id, t));
        }
    }

    /// <summary>Replaces media rows (skips entries with empty URL).</summary>
    public void ReplaceMedia(IReadOnlyList<(string Url, string Type)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _media.Clear();
        foreach (var (url, type) in items)
        {
            var u = url?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(u))
                continue;

            var ty = NormalizeMediaType(type);
            _media.Add(new ExerciseMedia(Id, ty, u));
        }
    }

    /// <summary>Replaces warnings (non-empty lines only).</summary>
    public void ReplaceWarnings(IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        _warnings.Clear();
        foreach (var w in warnings)
        {
            var c = w?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(c))
                continue;

            _warnings.Add(new ExerciseWarning(Id, c));
        }
    }

    private static string NormalizeMediaType(string? type)
    {
        return string.Equals(type?.Trim(), "Video", StringComparison.OrdinalIgnoreCase) ? "Video" : "Image";
    }
}