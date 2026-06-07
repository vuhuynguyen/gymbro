namespace Modules.UserModule.Application.DTOs;

public class InviteCodeDto
{
    public string Code { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public bool IsExpired { get; set; }
}
