using System.Security.Claims;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;
using NSubstitute;
using WebApi.Middleware;
using Xunit;

namespace Gymbro.Tests.Authorization;

/// <summary>
/// Unit coverage for <c>TenantResolutionMiddleware</c>. The X-Tenant-Id spoof
/// rejection path runs without a database — the membership repository is an
/// NSubstitute fake — and proves the middleware only accepts a tenant the caller actually belongs to,
/// so a spoofed/foreign header resolves to NO tenant (closing the EF-filter and authorization paths).
/// </summary>
public sealed class TenantResolutionMiddlewareTests
{
    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly Guid MemberTenant = Guid.NewGuid();
    private static readonly Guid ForeignTenant = Guid.NewGuid();

    [Fact]
    public async Task A_tenant_the_caller_belongs_to_is_accepted()
    {
        var ctx = await InvokeWithHeaderAsync(MemberTenant, isAdmin: false);

        Assert.True(ctx.Items.TryGetValue(TenantConstants.ValidatedTenantIdItemKey, out var value));
        Assert.Equal(MemberTenant, value);
    }

    [Fact]
    public async Task A_foreign_tenant_header_is_rejected_and_never_stored()
    {
        var ctx = await InvokeWithHeaderAsync(ForeignTenant, isAdmin: false);

        // The membership lookup returns null, so the middleware leaves the validated-tenant slot empty →
        // CurrentUser.TenantId resolves to null → tenant-scoped EF queries match nothing and the
        // AuthorizationBehavior denies. Spoofing the header buys the attacker nothing.
        Assert.False(ctx.Items.ContainsKey(TenantConstants.ValidatedTenantIdItemKey));
    }

    [Fact]
    public async Task A_platform_admin_bypasses_the_membership_check()
    {
        var ctx = await InvokeWithHeaderAsync(ForeignTenant, isAdmin: true);

        Assert.True(ctx.Items.TryGetValue(TenantConstants.ValidatedTenantIdItemKey, out var value));
        Assert.Equal(ForeignTenant, value);
    }

    [Fact]
    public async Task An_unauthenticated_request_resolves_no_tenant()
    {
        var ctx = await InvokeWithHeaderAsync(MemberTenant, isAdmin: false, authenticated: false);

        Assert.False(ctx.Items.ContainsKey(TenantConstants.ValidatedTenantIdItemKey));
    }

    [Fact]
    public async Task A_malformed_header_resolves_no_tenant()
    {
        var ctx = BuildContext(isAdmin: false, authenticated: true);
        ctx.Request.Headers["X-Tenant-Id"] = "not-a-guid";

        await new TenantResolutionMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.False(ctx.Items.ContainsKey(TenantConstants.ValidatedTenantIdItemKey));
    }

    private static async Task<HttpContext> InvokeWithHeaderAsync(
        Guid headerTenant, bool isAdmin, bool authenticated = true)
    {
        var ctx = BuildContext(isAdmin, authenticated);
        ctx.Request.Headers["X-Tenant-Id"] = headerTenant.ToString();

        await new TenantResolutionMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);
        return ctx;
    }

    private static DefaultHttpContext BuildContext(bool isAdmin, bool authenticated)
    {
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        // The caller is a member of MemberTenant only; every other tenant returns null (no membership).
        roleRepository
            .GetByUserAndTenantAsync(CallerId, MemberTenant, Arg.Any<CancellationToken>())
            .Returns(UserTenantRole.Create(CallerId, MemberTenant, TenantRole.Owner));

        var services = new ServiceCollection();
        services.AddSingleton(roleRepository);
        services.AddSingleton(Substitute.For<IRequestRoleCache>());

        var claims = new List<Claim>
        {
            new("domainUserId", CallerId.ToString()),
            new("is_admin", isAdmin ? "true" : "false"),
        };
        // A ClaimsIdentity with an authenticationType reports IsAuthenticated == true; without one, false.
        var identity = authenticated ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity(claims);

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(identity),
        };
    }
}
