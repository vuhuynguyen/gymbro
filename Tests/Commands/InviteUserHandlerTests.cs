using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.Commands;
using Modules.UserModule.Application.Commands.Handlers;
using Modules.UserModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// "At most one active invite per email per tenant." The handler short-circuits with a Conflict when an
/// active invite already exists for the email in the current tenant; otherwise it mints a fresh invite
/// (Role.Client, 7-day expiry) for the resolved tenant, persists it, commits once, and returns the new
/// invite code. Fully mocked — no database.
/// </summary>
public sealed class InviteUserHandlerTests
{
    private static InviteUserHandler CreateSut(
        IInviteRepository inviteRepository,
        IUnitOfWork unitOfWork,
        Guid tenantId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        return new InviteUserHandler(inviteRepository, tenantContext, unitOfWork);
    }

    [Fact]
    public async Task Existing_active_invite_for_email_returns_conflict_before_any_insert()
    {
        var tenantId = Guid.NewGuid();
        const string email = "client@example.com";

        var inviteRepository = Substitute.For<IInviteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // An active invite already exists for this email in this tenant → pre-check short-circuits.
        var existing = Invite.Create(email, tenantId, TenantRole.Client, DateTimeOffset.UtcNow.AddDays(7));
        inviteRepository.GetActiveByEmailAndTenantAsync(email, tenantId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var sut = CreateSut(inviteRepository, unitOfWork, tenantId);

        var result = await sut.Handle(new InviteUserCommand(email), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        // Rejected before touching persistence.
        await inviteRepository.DidNotReceive().AddAsync(Arg.Any<Invite>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_email_creates_client_invite_for_tenant_and_returns_code()
    {
        var tenantId = Guid.NewGuid();
        const string email = "newclient@example.com";

        var inviteRepository = Substitute.For<IInviteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // No active invite yet → the handler mints a new one.
        inviteRepository.GetActiveByEmailAndTenantAsync(email, tenantId, Arg.Any<CancellationToken>())
            .Returns((Invite?)null);

        var sut = CreateSut(inviteRepository, unitOfWork, tenantId);

        var result = await sut.Handle(new InviteUserCommand(email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var code = result.Value!;
        Assert.False(string.IsNullOrWhiteSpace(code));

        // Persisted: a client invite scoped to the resolved tenant, with the returned code, then one commit.
        await inviteRepository.Received(1).AddAsync(
            Arg.Is<Invite>(i =>
                i.Email == email &&
                i.TenantId == tenantId &&
                i.Role == TenantRole.Client &&
                !i.IsUsed &&
                i.Code == code),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
