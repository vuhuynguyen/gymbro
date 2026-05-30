using Microsoft.AspNetCore.Identity;

namespace Modules.IdentityModule.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public Guid DomainUserId { get; private set; }
    public bool IsPlatformAdmin { get; private set; }

    public void SetDomainUserId(Guid domainUserId)
    {
        DomainUserId = domainUserId;
    }

    public void SetPlatformAdmin(bool value)
    {
        IsPlatformAdmin = value;
    }
}
