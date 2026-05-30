using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Authorization;

public sealed class AuthorizationBehaviorTests
{
    private sealed record DeniedRequest(Permission RequiredPermission)
        : IRequest<Result>, ITenantAuthorizedRequest;

    private sealed record AllowedRequest(Permission RequiredPermission)
        : IRequest<Result<Guid>>, ITenantAuthorizedRequest;

    private sealed record PassthroughRequest : IRequest<Result>;

    [Fact]
    public async Task Non_tenant_authorized_request_invokes_handler_without_auth_calls()
    {
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var tenantContext = Substitute.For<ITenantContext>();
        var invoked = false;

        var behavior = new AuthorizationBehavior<PassthroughRequest, Result>(tenantAuth, tenantContext);
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
        await tenantAuth.DidNotReceive()
            .HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Permission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_tenant_returns_validation_failure_for_Result()
    {
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns((Guid?)null);

        var behavior = new AuthorizationBehavior<DeniedRequest, Result>(tenantAuth, tenantContext);
        var result = await behavior.Handle(
            new DeniedRequest(Permission.PlanView),
            _ => throw new InvalidOperationException("Handler should not run."),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantId", result.Error.Code);
        await tenantAuth.DidNotReceive()
            .HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Permission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Denied_permission_returns_unauthorized_for_Result_of_T()
    {
        var tenantId = Guid.NewGuid();
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantAuth.HasPermissionAsync(tenantId, Permission.PlanAssign, Arg.Any<CancellationToken>())
            .Returns(false);

        var behavior = new AuthorizationBehavior<AllowedRequest, Result<Guid>>(tenantAuth, tenantContext);
        var result = await behavior.Handle(
            new AllowedRequest(Permission.PlanAssign),
            _ => throw new InvalidOperationException("Handler should not run."),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        Assert.Equal(TenantPermissionMessages.UnauthorizedTemplate, result.Error.Message);
    }

    [Fact]
    public async Task Granted_permission_invokes_next()
    {
        var tenantId = Guid.NewGuid();
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantAuth.HasPermissionAsync(tenantId, Permission.PlanCreate, Arg.Any<CancellationToken>())
            .Returns(true);

        var behavior = new AuthorizationBehavior<AllowedRequest, Result<Guid>>(tenantAuth, tenantContext);
        var result = await behavior.Handle(
            new AllowedRequest(Permission.PlanCreate),
            _ => Task.FromResult(Result<Guid>.Success(Guid.NewGuid())),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        await tenantAuth.Received(1)
            .HasPermissionAsync(tenantId, Permission.PlanCreate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_user_id_with_denied_permission_matches_non_member_outcome()
    {
        var tenantId = Guid.NewGuid();
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantAuth.HasPermissionAsync(tenantId, Permission.PlanView, Arg.Any<CancellationToken>())
            .Returns(false);

        var behavior = new AuthorizationBehavior<AllowedRequest, Result<Guid>>(tenantAuth, tenantContext);
        var result = await behavior.Handle(
            new AllowedRequest(Permission.PlanView),
            _ => throw new InvalidOperationException("Handler should not run."),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }
}
