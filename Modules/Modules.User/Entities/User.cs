using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.UserModule.Entities;

public class User : AggregateRoot, ISoftDelete
{
    public string Name { get; private set; } = null!;

    /// <summary>The user's IANA time-zone id (e.g. "America/Toronto") — the authoritative anchor for their
    /// calendar day/week boundaries across every gym. Null until a client reports it.</summary>
    public string? TimeZoneId { get; private set; }

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

    /// <summary>Sets the user's IANA time-zone (validated); null/blank clears it.</summary>
    public void SetTimeZone(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
        {
            TimeZoneId = null;
            return;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out _))
            throw new DomainException("Unknown time-zone id.");

        TimeZoneId = ianaId.Trim();
    }
}
