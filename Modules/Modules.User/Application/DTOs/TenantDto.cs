namespace Modules.UserModule.Application.DTOs;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; }
    public int MemberCount { get; set; }
    public string? OwnerName { get; set; }
}
