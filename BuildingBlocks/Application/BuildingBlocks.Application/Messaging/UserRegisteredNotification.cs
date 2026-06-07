namespace BuildingBlocks.Application.Messaging;

public record UserRegisteredNotification(Guid DomainUserId, string Email, string? FullName = null)
    : ICrossModuleNotification;
