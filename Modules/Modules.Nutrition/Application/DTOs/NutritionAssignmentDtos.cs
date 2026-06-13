using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.DTOs;

public sealed record NutritionAssignmentSummaryDto(
    Guid Id,
    Guid TraineeId,
    Guid PlanId,
    int PlanVersion,
    /// <summary>Latest PUBLISHED version of the plan's template; equals PlanVersion when nothing newer is published.</summary>
    int LatestPlanVersion,
    /// <summary>True when a newer published version exists — drives the "New vX" badge + apply-latest action.</summary>
    bool HasNewerVersion,
    string PlanName,
    DateOnly StartDate,
    DateOnly? EndDate,
    NutritionVisibilityMode VisibilityMode,
    bool HideMacroTargets,
    bool DisableTraineeEditing,
    bool IsActive);

public sealed record NutritionAssignmentListDto(
    IReadOnlyList<NutritionAssignmentSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
