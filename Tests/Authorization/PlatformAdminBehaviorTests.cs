using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Authorization;

public sealed class PlatformAdminBehaviorTests
{
    private sealed record AdminRequest : IRequest<Result>, IPlatformAdminRequest;

    private sealed record AdminRequestOfT : IRequest<Result<Guid>>, IPlatformAdminRequest;

    private sealed record PassthroughRequest : IRequest<Result>;

    [Fact]
    public async Task Non_platform_admin_request_invokes_handler_without_admin_check()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        var invoked = false;

        var behavior = new PlatformAdminBehavior<PassthroughRequest, Result>(currentUser);
        var result = await behavior.Handle(
            new PassthroughRequest(),
            _ =>
            {
                invoked = true;
                return Task.FromResult(Result.Success());
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Non_admin_caller_returns_admin_only_failure_for_Result()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAdmin.Returns(false);

        var behavior = new PlatformAdminBehavior<AdminRequest, Result>(currentUser);
        var result = await behavior.Handle(
            new AdminRequest(),
            _ => throw new InvalidOperationException("Handler should not run."),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AdminOnly", result.Error.Code);
    }

    [Fact]
    public async Task Non_admin_caller_returns_admin_only_failure_for_Result_of_T()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAdmin.Returns(false);

        var behavior = new PlatformAdminBehavior<AdminRequestOfT, Result<Guid>>(currentUser);
        var result = await behavior.Handle(
            new AdminRequestOfT(),
            _ => throw new InvalidOperationException("Handler should not run."),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AdminOnly", result.Error.Code);
    }

    [Fact]
    public async Task Admin_caller_invokes_next()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAdmin.Returns(true);
        var invoked = false;

        var behavior = new PlatformAdminBehavior<AdminRequest, Result>(currentUser);
        var result = await behavior.Handle(
            new AdminRequest(),
            _ =>
            {
                invoked = true;
                return Task.FromResult(Result.Success());
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.True(result.IsSuccess);
    }
}
