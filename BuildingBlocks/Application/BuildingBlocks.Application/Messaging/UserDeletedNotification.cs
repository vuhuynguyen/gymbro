namespace BuildingBlocks.Application.Messaging;

public record UserDeletedNotification(Guid DomainUserId) : ICrossModuleNotification;
