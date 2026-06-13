namespace BuildingBlocks.Shared.DomainPrimitives;

/// <summary>
/// A draft-first, versioned plan template (workout or nutrition): a single mutable draft head absorbs edits and
/// <c>Publish()</c> promotes it to an immutable version. Exists so the shared template-lifecycle guards
/// (<see cref="Plans.PlanLifecycle"/>) can be written once for both modules over a common shape — without the
/// modules referencing each other's entities (they implement this kernel interface, the modular-monolith pattern).
/// </summary>
public interface IVersionedPlan
{
    Guid Id { get; }
    int Version { get; }
    bool IsDraft { get; }
    bool IsArchived { get; }
}
