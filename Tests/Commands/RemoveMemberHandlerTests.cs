using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
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
/// Regression for S1: a Client must not be able to remove members. The handler gates on
/// <see cref="Permission.ClientRemove"/> (Owner-only), so a caller whose role lacks it is denied
/// before any repository lookup or mutation. Fully mocked — no database.
/// </summary>
public sealed class RemoveMemberHandlerTests
{
    [Fact]
    public async Task Client_without_ClientRemove_is_denied()
    {
        var tenantId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var currentUser = Substitute.For<ICurrentUser>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        currentUser.UserId.Returns(Guid.NewGuid());
        // A Client lacks ClientRemove → permission check fails.
        tenantAuth.HasPermissionAsync(tenantId, Permission.ClientRemove, Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = new RemoveMemberHandler(tenantAuth, roleRepository, currentUser, unitOfWork);

        var result = await sut.Handle(new RemoveMemberCommand(tenantId, targetUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);

        // Denied before touching persistence.
        await roleRepository.DidNotReceive()
            .GetByUserAndTenantAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        roleRepository.DidNotReceive().Remove(Arg.Any<UserTenantRole>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
