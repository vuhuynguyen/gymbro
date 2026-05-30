using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.UserModule.Entities;

public class User : AggregateRoot, ISoftDelete
{
    public string Name { get; private set; } = null!;

    private User() { }

    public static User Create(Guid domainUserId, string name)
    {
        if (domainUserId == Guid.Empty)
            throw new ArgumentException("DomainUserId is required.", nameof(domainUserId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        return new User
        {
            Id = domainUserId,
            Name = name.Trim()
        };
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
    }
}
