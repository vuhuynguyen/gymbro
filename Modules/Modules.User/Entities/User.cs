using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.UserModule.Entities;

public class User : AggregateRoot, ISoftDelete
{
    public string Name { get; private set; } = null!;

    private User() { }

    public static User Create(Guid domainUserId, string name)
    {
        if (domainUserId == Guid.Empty)
            throw new DomainException("DomainUserId is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");

        return new User
        {
            Id = domainUserId,
            Name = name.Trim()
        };
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");
        Name = name.Trim();
    }
}
