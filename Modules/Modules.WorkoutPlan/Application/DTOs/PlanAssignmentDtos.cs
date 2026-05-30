using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.DTOs;

public sealed record PlanAssignmentSummaryDto(
    Guid Id,
    Guid TraineeId,
    Guid PlanId,
    int PlanVersion,
    int LatestPlanVersion,
    bool HasNewerVersion,
    DateOnly StartDate,
    int FrequencyDaysPerWeek,
    PlanVisibilityMode VisibilityMode,
    bool IsCustomized);

public sealed record PlanAssignmentListDto(
    IReadOnlyList<PlanAssignmentSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
