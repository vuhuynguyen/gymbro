namespace Modules.UserModule.Application.DTOs;

public class AdminTenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid OwnerUserId { get; set; }
    public string OwnerName { get; set; } = null!;
    public int MemberCount { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }
}
