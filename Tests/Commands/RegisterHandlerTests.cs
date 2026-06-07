using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Messaging;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Application.Commands;
using Modules.IdentityModule.Application.Commands.Handlers;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Cross-store atomicity for registration. The Identity <c>AppUser</c> (DB3) and
/// the domain User/Tenant/Owner-role provisioning (DB1, via <c>UserRegisteredNotification</c>) must land
/// together inside one <see cref="ICrossStoreTransaction"/>. These verify the handler never commits when
/// the work before <c>CommitAsync</c> fails, so the scope's disposal rolls the half-created AppUser back
/// instead of leaving a login with no workspace.
///
/// Both cases fail before <c>CommitAsync</c>, which is the only point the token services are used — so
/// <see cref="TokenService"/> / <see cref="RefreshTokenService"/> are never reached and are passed as
/// <c>null</c>. Fully mocked — no database.
/// </summary>
public sealed class RegisterHandlerTests
{
    private static UserManager<AppUser> MockUserManager()
    {
        // Standard NSubstitute pattern: only the virtual UserManager methods we stub are exercised.
        var store = Substitute.For<IUserStore<AppUser>>();
        return Substitute.For<UserManager<AppUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static RegisterCommand Command() => new("jane@example.com", "P@ssw0rd!", "Jane Doe");

    [Fact]
    public async Task Provisioning_failure_does_not_commit_and_disposes_scope()
    {
        var userManager = MockUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);
        userManager.CreateAsync(Arg.Any<AppUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        userManager.UpdateAsync(Arg.Any<AppUser>()).Returns(IdentityResult.Success);

        var publisher = Substitute.For<IPublisher>();
        // The cross-store reaction (create domain User + Tenant + Owner role) fails after CreateAsync.
        publisher.Publish(Arg.Any<UserRegisteredNotification>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("provisioning store unreachable"));

        var scope = Substitute.For<ICrossStoreTransactionScope>();
        var crossStore = Substitute.For<ICrossStoreTransaction>();
        crossStore.BeginAsync(Arg.Any<CancellationToken>()).Returns(scope);

        var sut = new RegisterHandler(userManager, tokenService: null!, refreshTokenService: null!,
            publisher, crossStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(Command(), CancellationToken.None));

        // Never committed → disposing the uncommitted scope reverts the AppUser too (no orphaned login).
        await scope.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await scope.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Weak_password_fails_validation_without_committing_or_provisioning()
    {
        var userManager = MockUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);
        userManager.CreateAsync(Arg.Any<AppUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var publisher = Substitute.For<IPublisher>();

        var scope = Substitute.For<ICrossStoreTransactionScope>();
        var crossStore = Substitute.For<ICrossStoreTransaction>();
        crossStore.BeginAsync(Arg.Any<CancellationToken>()).Returns(scope);

        var sut = new RegisterHandler(userManager, tokenService: null!, refreshTokenService: null!,
            publisher, crossStore);

        var result = await sut.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);

        // A rejected CreateAsync must not provision the domain side nor commit; the scope rolls back.
        await publisher.DidNotReceive()
            .Publish(Arg.Any<UserRegisteredNotification>(), Arg.Any<CancellationToken>());
        await scope.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await scope.Received(1).DisposeAsync();
    }
}
