namespace Modules.UserModule.Application.DTOs;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedOnUtc { get; set; }
}
