using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.DTOs;

public sealed record NutritionAssignmentSummaryDto(
    Guid Id,
    Guid TraineeId,
    Guid PlanId,
    int PlanVersion,
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
