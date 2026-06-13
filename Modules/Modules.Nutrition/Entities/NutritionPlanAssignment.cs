using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// Pins one nutrition-plan version to a trainee with a point-in-time snapshot and visibility controls.
/// Direct port of <c>PlanAssignment</c>. The daily log reads the active assignment for a date to snapshot
/// that day's planned meals.
/// </summary>
public sealed class NutritionPlanAssignment : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TraineeId { get; private set; }
    public Guid PlanId { get; private set; }
    public int PlanVersion { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public NutritionVisibilityMode VisibilityMode { get; private set; }
    public bool HideMacroTargets { get; private set; }
    public bool DisableTraineeEditing { get; private set; }
    public bool IsActive { get; private set; }
    public string? SnapshotJson { get; private set; }

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private NutritionPlanAssignment() { }

    public static NutritionPlanAssignment Create(
        Guid tenantId,
        Guid createdBy,
        Guid traineeId,
        Guid planId,
        int planVersion,
        DateOnly startDate,
        DateOnly? endDate,
        NutritionVisibilityMode visibilityMode,
        bool hideMacroTargets,
        bool disableTraineeEditing,
        string? snapshotJson)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");
        if (createdBy == Guid.Empty) throw new DomainException("CreatedBy is required.");
        if (traineeId == Guid.Empty) throw new DomainException("TraineeId is required.");
        if (planId == Guid.Empty) throw new DomainException("PlanId is required.");
        if (planVersion < 1) throw new DomainException("planVersion is out of range.");
        if (endDate.HasValue && endDate.Value < startDate)
            throw new DomainException("endDate cannot precede startDate.");

        return new NutritionPlanAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedBy = createdBy,
            TraineeId = traineeId,
            PlanId = planId,
            PlanVersion = planVersion,
            StartDate = startDate,
            EndDate = endDate,
            VisibilityMode = visibilityMode,
            HideMacroTargets = hideMacroTargets,
            DisableTraineeEditing = disableTraineeEditing,
            IsActive = true,
            SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim(),
            IsDeleted = false
        };
    }

    /// <summary>True when this assignment governs the given date (active, within [StartDate, EndDate]).</summary>
    public bool AppliesOn(DateOnly date) =>
        IsActive && date >= StartDate && (EndDate is null || date <= EndDate.Value);

    /// <summary>Pause (deactivate) or resume (reactivate) the assignment. Mirrors <c>PlanAssignment.SetActive</c>.</summary>
    public void SetActive(bool active) => IsActive = active;

    /// <summary>
    /// Re-points the assignment to a newer published plan version with a fresh snapshot (apply-latest). Mirrors
    /// <c>PlanAssignment.ApplyNewVersion</c>.
    /// </summary>
    public void ApplyNewVersion(Guid planId, int planVersion, string? snapshotJson)
    {
        if (planId == Guid.Empty) throw new DomainException("PlanId is required.");
        if (planVersion < 1) throw new DomainException("planVersion is out of range.");
        PlanId = planId;
        PlanVersion = planVersion;
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim();
    }

    /// <summary>
    /// Edits the assignment's configuration in place, keeping the pinned plan version + snapshot. A null
    /// <paramref name="startDate"/> leaves the existing start date unchanged. Mirrors
    /// <c>PlanAssignment.UpdateConfiguration</c>, adapted to nutrition's fields (end date + macro hiding).
    /// </summary>
    public void UpdateConfiguration(
        DateOnly? startDate,
        DateOnly? endDate,
        NutritionVisibilityMode visibilityMode,
        bool hideMacroTargets,
        bool disableTraineeEditing)
    {
        var effectiveStart = startDate ?? StartDate;
        if (endDate.HasValue && endDate.Value < effectiveStart)
            throw new DomainException("endDate cannot precede startDate.");

        if (startDate.HasValue)
            StartDate = startDate.Value;

        EndDate = endDate;
        VisibilityMode = visibilityMode;
        HideMacroTargets = hideMacroTargets;
        DisableTraineeEditing = disableTraineeEditing;
    }
}
