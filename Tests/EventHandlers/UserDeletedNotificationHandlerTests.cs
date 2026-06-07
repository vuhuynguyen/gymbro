using BuildingBlocks.Application.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Modules.IdentityModule.Application.Abstractions;
using Modules.IdentityModule.Application.EventHandlers;
using Modules.IdentityModule.Infrastructure.Identity;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.EventHandlers;

public sealed class UserDeletedNotificationHandlerTests
{
    [Fact]
    public async Task Successful_delete_evicts_the_cached_security_stamp()
    {
        var appUserId = Guid.NewGuid();
        var domainUserId = Guid.NewGuid();
        var appUser = new AppUser
        {
            Id = appUserId,
            UserName = "jane@example.com",
            Email = "jane@example.com"
        };
        appUser.SetDomainUserId(domainUserId);

        var userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
        userManager.Users.Returns(new[] { appUser }.AsQueryable());
        userManager.DeleteAsync(appUser).Returns(IdentityResult.Success);

        var stampCache = Substitute.For<ISecurityStampCacheService>();
        var logger = Substitute.For<ILogger<UserDeletedNotificationHandler>>();

        var sut = new UserDeletedNotificationHandler(userManager, stampCache, logger);

        await sut.Handle(new UserDeletedNotification(domainUserId), CancellationToken.None);

        await stampCache.Received(1).EvictAsync(appUserId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_app_user_is_idempotent_and_does_not_evict()
    {
        var userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
        userManager.Users.Returns(Array.Empty<AppUser>().AsQueryable());

        var stampCache = Substitute.For<ISecurityStampCacheService>();
        var logger = Substitute.For<ILogger<UserDeletedNotificationHandler>>();

        var sut = new UserDeletedNotificationHandler(userManager, stampCache, logger);

        await sut.Handle(new UserDeletedNotification(Guid.NewGuid()), CancellationToken.None);

        await stampCache.DidNotReceive().EvictAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await userManager.DidNotReceive().DeleteAsync(Arg.Any<AppUser>());
    }
}
