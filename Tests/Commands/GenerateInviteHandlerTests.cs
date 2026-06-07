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
/// Pins the trainer-invite generation path: the handler stamps the invite with the caller's tenant
/// (from <see cref="ITenantContext"/>), defaults the role to <see cref="TenantRole.Client"/>, mints a
/// fresh bearer code, persists it via the repository plus a single SaveChanges, and surfaces that code.
/// Fully mocked — no database.
/// </summary>
public sealed class GenerateInviteHandlerTests
{
    private static GenerateInviteHandler CreateSut(
        IInviteRepository inviteRepository,
        IUnitOfWork unitOfWork,
        Guid tenantId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        return new GenerateInviteHandler(inviteRepository, tenantContext, unitOfWork);
    }

    [Fact]
    public async Task Generating_an_invite_returns_the_minted_code()
    {
        var tenantId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        Invite? captured = null;
        await inviteRepository.AddAsync(Arg.Do<Invite>(i => captured = i), Arg.Any<CancellationToken>());

        var sut = CreateSut(inviteRepository, unitOfWork, tenantId);

        var result = await sut.Handle(new GenerateInviteCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var code = result.Value!;

        // The returned code is exactly the persisted invite's code (a non-empty bearer secret).
        Assert.NotNull(captured);
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Equal(captured!.Code, code);
    }

    [Fact]
    public async Task Generating_an_invite_persists_a_tenant_scoped_client_invite_and_saves_once()
    {
        var tenantId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var sut = CreateSut(inviteRepository, unitOfWork, tenantId);

        var result = await sut.Handle(new GenerateInviteCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Persisted: an unused invite stamped with the caller's tenant, defaulting to the Client role,
        // expiring in the future — followed by exactly one SaveChanges.
        await inviteRepository.Received(1).AddAsync(
            Arg.Is<Invite>(i =>
                i.TenantId == tenantId &&
                i.Role == TenantRole.Client &&
                !i.IsUsed &&
                i.ExpiredAt > DateTimeOffset.UtcNow),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
