using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// Raised when a daily nutrition log closes (local-midnight rollover). Carries the finalized adherence so
/// downstream consumers (streaks, coach digests, push — all later phases) need no recompute. Goes through
/// the transactional outbox like <c>SessionCompletedEvent</c>. MVP has no handler beyond logging.
/// </summary>
public sealed record DailyLogClosedEvent(
    Guid DailyNutritionLogId,
    Guid TraineeId,
    Guid TenantId,
    DateOnly LocalDate,
    int AdherencePct,
    int MissedCount,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
