using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Messaging;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.Admin.Commands;
using Modules.UserModule.Application.Admin.Commands.Handlers;
using Modules.UserModule.Entities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Cross-store atomicity for admin user deletion. The domain <c>User</c> (DB1)
/// soft-delete and the Identity <c>AppUser</c> (DB3) hard-delete must commit or roll back together via
/// <see cref="ICrossStoreTransaction"/>. These verify the handler only commits on the happy path and
/// never commits when the Identity cleanup fails — so the scope's disposal rolls the domain delete back
/// instead of leaving an orphaned AppUser. Fully mocked — no database.
/// </summary>
public sealed class AdminDeleteUserHandlerTests
{
    private static (IUserRepository repo, IUnitOfWork uow, IMediator mediator,
        ICrossStoreTransaction crossStore, ICrossStoreTransactionScope scope) Mocks(User? user)
    {
        var repo = Substitute.For<IUserRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(user);

        var uow = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        var scope = Substitute.For<ICrossStoreTransactionScope>();
        var crossStore = Substitute.For<ICrossStoreTransaction>();
        crossStore.BeginAsync(Arg.Any<CancellationToken>()).Returns(scope);

        return (repo, uow, mediator, crossStore, scope);
    }

    [Fact]
    public async Task Identity_cleanup_failure_does_not_commit_and_disposes_scope()
    {
        var userId = Guid.NewGuid();
        var (repo, uow, mediator, crossStore, scope) = Mocks(User.Create(userId, "Jane"));

        // The cross-store reaction (delete the Identity AppUser) blows up after the domain delete was staged.
        mediator.Publish(Arg.Any<UserDeletedNotification>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("identity store unreachable"));

        var sut = new AdminDeleteUserHandler(repo, uow, mediator, crossStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(new AdminDeleteUserCommand(userId), CancellationToken.None));

        // Domain delete was staged...
        repo.Received(1).Remove(Arg.Any<User>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // ...but never committed — disposing the uncommitted scope rolls both stores back (no orphan).
        await scope.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await scope.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Successful_delete_commits_inside_the_cross_store_transaction()
    {
        var userId = Guid.NewGuid();
        var (repo, uow, mediator, crossStore, scope) = Mocks(User.Create(userId, "Jane"));

        var sut = new AdminDeleteUserHandler(repo, uow, mediator, crossStore);

        var result = await sut.Handle(new AdminDeleteUserCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Received.InOrder(() =>
        {
            crossStore.BeginAsync(Arg.Any<CancellationToken>());
            repo.Remove(Arg.Any<User>());
            uow.SaveChangesAsync(Arg.Any<CancellationToken>());
            mediator.Publish(Arg.Any<UserDeletedNotification>(), Arg.Any<CancellationToken>());
            scope.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Unknown_user_returns_NotFound_without_opening_a_transaction()
    {
        var (repo, uow, mediator, crossStore, _) = Mocks(user: null);

        var sut = new AdminDeleteUserHandler(repo, uow, mediator, crossStore);

        var result = await sut.Handle(new AdminDeleteUserCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        // Missing user is rejected before any store is touched.
        await crossStore.DidNotReceive().BeginAsync(Arg.Any<CancellationToken>());
        repo.DidNotReceive().Remove(Arg.Any<User>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
