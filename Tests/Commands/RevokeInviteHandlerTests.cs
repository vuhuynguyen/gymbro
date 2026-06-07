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
/// "An invite can be revoked once, while it is still live." The handler enforces two guards — an unknown
/// code (within the current tenant) is NotFound, and an already used/revoked invite is a Validation failure —
/// and on the happy path flips the invite to used and commits via the unit of work. Fully mocked — no database.
/// </summary>
public sealed class RevokeInviteHandlerTests
{
    private static RevokeInviteHandler CreateSut(
        IInviteRepository inviteRepository,
        IUnitOfWork unitOfWork,
        Guid tenantId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        return new RevokeInviteHandler(inviteRepository, tenantContext, unitOfWork);
    }

    private static Invite CreateInvite(Guid tenantId)
        => Invite.Create("trainee@gymbro.local", tenantId, TenantRole.Client, DateTimeOffset.UtcNow.AddDays(7));

    [Fact]
    public async Task Unknown_code_in_tenant_returns_not_found_and_never_persists()
    {
        var tenantId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // No invite matches the code within the caller's tenant.
        inviteRepository.GetByCodeAndTenantAsync("ABCD2345", tenantId, Arg.Any<CancellationToken>())
            .Returns((Invite?)null);

        var sut = CreateSut(inviteRepository, unitOfWork, tenantId);

        var result = await sut.Handle(new RevokeInviteCommand("ABCD2345"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Already_used_invite_returns_validation_and_never_persists()
    {
        var tenantId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // Invite already consumed/revoked → a second revoke must be rejected, not re-run.
        var invite = CreateInvite(tenantId);
        invite.MarkUsed();
        inviteRepository.GetByCodeAndTenantAsync(invite.Code, tenantId, Arg.Any<CancellationToken>())
            .Returns(invite);

        var sut = CreateSut(inviteRepository, unitOfWork, tenantId);

        var result = await sut.Handle(new RevokeInviteCommand(invite.Code), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Live_invite_is_marked_used_and_persisted_once()
    {
        var tenantId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var invite = CreateInvite(tenantId);
        inviteRepository.GetByCodeAndTenantAsync(invite.Code, tenantId, Arg.Any<CancellationToken>())
            .Returns(invite);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(inviteRepository, unitOfWork, tenantId);

        var result = await sut.Handle(new RevokeInviteCommand(invite.Code), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Domain state transitioned to used and the work was committed exactly once.
        Assert.True(invite.IsUsed);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
