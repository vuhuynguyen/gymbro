using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutSessionModule.Entities;

public sealed record SessionCompletedEvent(
    Guid SessionId,
    Guid TraineeId,
    Guid TenantId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
