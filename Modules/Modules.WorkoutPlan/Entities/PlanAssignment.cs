using BuildingBlocks.Shared.DomainPrimitives;
using Modules.WorkoutPlanModule.Application;

namespace Modules.WorkoutPlanModule.Entities;

public sealed class PlanAssignment : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TraineeId { get; private set; }
    public Guid PlanId { get; private set; }
    public int PlanVersion { get; private set; }
    public DateOnly StartDate { get; private set; }
    public int FrequencyDaysPerWeek { get; private set; }
    public PlanVisibilityMode VisibilityMode { get; private set; }
    public bool HideExercises { get; private set; }
    public bool HideSetsReps { get; private set; }
    public bool HideFutureWorkouts { get; private set; }
    public bool DisableTraineeEditing { get; private set; }
    /// <summary>Paused assignments are kept (history preserved) but hidden from the trainee's start-workout picker.</summary>
    public bool IsActive { get; private set; }
    public string? SnapshotJson { get; private set; }

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PlanAssignment()
    {
    }

    public static PlanAssignment Create(
        Guid tenantId,
        Guid createdBy,
        Guid traineeId,
        Guid planId,
        int planVersion,
        DateOnly startDate,
        int frequencyDaysPerWeek,
        PlanVisibilityMode visibilityMode,
        bool hideExercises,
        bool hideSetsReps,
        bool hideFutureWorkouts,
        bool disableTraineeEditing,
        string? snapshotJson)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdBy == Guid.Empty) throw new ArgumentException("CreatedBy is required.", nameof(createdBy));
        if (traineeId == Guid.Empty) throw new ArgumentException("TraineeId is required.", nameof(traineeId));
        if (planId == Guid.Empty) throw new ArgumentException("PlanId is required.", nameof(planId));
        if (planVersion < 1) throw new ArgumentOutOfRangeException(nameof(planVersion));
        if (frequencyDaysPerWeek < 1 || frequencyDaysPerWeek > 7)
            throw new ArgumentOutOfRangeException(nameof(frequencyDaysPerWeek));

        return new PlanAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedBy = createdBy,
            TraineeId = traineeId,
            PlanId = planId,
            PlanVersion = planVersion,
            StartDate = startDate,
            FrequencyDaysPerWeek = frequencyDaysPerWeek,
            VisibilityMode = visibilityMode,
            HideExercises = hideExercises,
            HideSetsReps = hideSetsReps,
            HideFutureWorkouts = hideFutureWorkouts,
            DisableTraineeEditing = disableTraineeEditing,
            IsActive = true,
            SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim(),
            IsDeleted = false
        };
    }

    public void SetActive(bool active)
    {
        IsActive = active;
    }

    public void ApplyNewVersion(Guid planId, int planVersion, string? snapshotJson)
    {
        if (planId == Guid.Empty) throw new ArgumentException("PlanId is required.", nameof(planId));
        if (planVersion < 1) throw new ArgumentOutOfRangeException(nameof(planVersion));
        PlanId = planId;
        PlanVersion = planVersion;
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim();
    }

    public void UpdateConfiguration(
        DateOnly? startDate,
        int frequencyDaysPerWeek,
        PlanVisibilityMode visibilityMode,
        bool hideExercises,
        bool hideSetsReps,
        bool hideFutureWorkouts,
        bool disableTraineeEditing)
    {
        if (frequencyDaysPerWeek < 1 || frequencyDaysPerWeek > 7)
            throw new ArgumentOutOfRangeException(nameof(frequencyDaysPerWeek));

        if (startDate.HasValue)
            StartDate = startDate.Value;

        FrequencyDaysPerWeek = frequencyDaysPerWeek;
        VisibilityMode = visibilityMode;
        HideExercises = hideExercises;
        HideSetsReps = hideSetsReps;
        HideFutureWorkouts = hideFutureWorkouts;
        DisableTraineeEditing = disableTraineeEditing;
    }
}
