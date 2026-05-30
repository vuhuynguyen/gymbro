namespace Modules.UserModule.Application.DTOs;

public class MemberDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; }
}
