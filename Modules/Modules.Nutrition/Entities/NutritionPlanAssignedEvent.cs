using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// Raised when a nutrition plan is assigned to a trainee. The seam for the reminders phase (eagerly seed today's
/// log / schedule reminders) and coach digests — future consumers attach to it without new eventing
/// infrastructure. Goes through the transactional outbox like <c>SessionCompletedEvent</c>; no handler today.
/// </summary>
public sealed record NutritionPlanAssignedEvent(
    Guid AssignmentId,
    Guid TraineeId,
    Guid TenantId,
    Guid PlanId,
    int PlanVersion,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
