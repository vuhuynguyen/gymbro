using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.UserModule.Entities;

public class Tenant : AggregateRoot, ISoftDelete
{
    public string Name { get; private set; } = null!;
    public Guid OwnerUserId { get; private set; }

    private Tenant() { }

    public static Tenant Create(string name, Guid ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("OwnerUserId is required.", nameof(ownerUserId));

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            OwnerUserId = ownerUserId
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
    }
}
