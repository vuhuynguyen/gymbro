using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Application.Abstractions;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Notifications;

public class UserRegisteredNotificationHandler(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    IUserTenantRoleRepository roleRepository,
    IUnitOfWork unitOfWork)
    : INotificationHandler<UserRegisteredNotification>
{
    public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
    {
        var existing = await userRepository.GetByIdAsync(notification.DomainUserId, cancellationToken);
        if (existing != null)
            return;

        var displayName = string.IsNullOrWhiteSpace(notification.FullName)
            ? notification.Email.Trim()
            : notification.FullName.Trim();
        var user = User.Create(notification.DomainUserId, displayName);
        await userRepository.AddAsync(user, cancellationToken);

        var tenant = Tenant.Create(BuildDefaultTenantName(notification), notification.DomainUserId);
        await tenantRepository.AddAsync(tenant, cancellationToken);

        var ownerRole = UserTenantRole.Create(notification.DomainUserId, tenant.Id, TenantRole.Owner);
        await roleRepository.AddAsync(ownerRole, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string BuildDefaultTenantName(UserRegisteredNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.FullName))
            return $"{notification.FullName.Trim()}'s workspace";

        var email = notification.Email.Trim();
        var at = email.IndexOf('@');
        if (at > 0)
            return $"{email[..at]}'s workspace";

        return "Personal workspace";
    }
}
