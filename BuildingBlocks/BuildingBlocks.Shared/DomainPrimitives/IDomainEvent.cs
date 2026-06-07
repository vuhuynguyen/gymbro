using MediatR;

namespace BuildingBlocks.Shared.DomainPrimitives;

/// <summary>
/// Marker for domain events. Extends MediatR's <see cref="INotification"/> so
/// <c>AppDbContext.SaveChangesAsync</c> can publish them via <c>IPublisher</c>
/// (MediatR 12+ requires the runtime type to implement <c>INotification</c>).
/// </summary>
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredOnUtc { get; }
}
