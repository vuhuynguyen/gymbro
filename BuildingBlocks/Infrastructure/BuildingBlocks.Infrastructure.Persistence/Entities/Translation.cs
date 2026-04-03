using BuildingBlocks.Shared.DomainPrimitives;

namespace BuildingBlocks.Infrastructure.Persistence.Entities;

/// <summary>
/// Shared translation row. Links to any entity by <see cref="EntityId"/> + <see cref="EntityType"/> string — no navigation properties to module aggregates.
/// </summary>
public class Translation : BaseEntity
{
    public Guid EntityId { get; private set; }
    public string EntityType { get; private set; }

    public string Language { get; private set; }
    public string Key { get; private set; }
    public string Value { get; private set; }
}