namespace Modules.UserModule.Application.DTOs;

public sealed record MeDto(
    Guid UserId,
    string Name,
    string? Email,
    bool IsPlatformAdmin);
