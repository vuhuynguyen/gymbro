using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Authorization;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.Admin.Commands;
using Modules.UserModule.Application.Admin.Commands.Handlers;
using Modules.UserModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The admin member-removal path must honour the same orphan-prevention invariant as LeaveTenant: removing
/// the last Owner is rejected so a tenant can never be left unadministrable, while a co-Owner or ordinary
/// member is removed and persisted. Fully mocked — the real per-tenant advisory lock is integration-level.
/// (Audit finding 5.)
/// </summary>
public sealed class AdminRemoveMemberHandlerTests
{
    private static AdminRemoveMemberHandler CreateSut(
        IUserTenantRoleRepository roleRepository,
        IUnitOfWork unitOfWork)
    {
        // Invoke the transactional action inline so the mocked-repository tests exercise the handler body.
        unitOfWork.ExecuteTransactionalAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<Task>)ci[0]).Invoke());

        return new AdminRemoveMemberHandler(roleRepository, unitOfWork);
    }

    [Fact]
    public async Task Non_member_gets_not_found_and_nothing_is_persisted()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        roleRepository.GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns((UserTenantRole?)null);

        var sut = CreateSut(roleRepository, unitOfWork);

        var result = await sut.Handle(new AdminRemoveMemberCommand(tenantId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        roleRepository.DidNotReceive().Remove(Arg.Any<UserTenantRole>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Removing_the_last_owner_is_rejected_and_nothing_is_persisted()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var owner = UserTenantRole.Create(ownerId, tenantId, TenantRole.Owner);
        roleRepository.GetByUserAndTenantAsync(ownerId, tenantId, Arg.Any<CancellationToken>())
            .Returns(owner);

        // The only Owner (plus an unrelated Client) → removal would orphan ownership.
        var client = UserTenantRole.Create(Guid.NewGuid(), tenantId, TenantRole.Client);
        roleRepository.GetByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<UserTenantRole> { owner, client });

        var sut = CreateSut(roleRepository, unitOfWork);

        var result = await sut.Handle(new AdminRemoveMemberCommand(tenantId, ownerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);
        roleRepository.DidNotReceive().Remove(Arg.Any<UserTenantRole>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_with_a_co_owner_is_removed_and_persisted()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var owner = UserTenantRole.Create(ownerId, tenantId, TenantRole.Owner);
        var coOwner = UserTenantRole.Create(Guid.NewGuid(), tenantId, TenantRole.Owner);
        roleRepository.GetByUserAndTenantAsync(ownerId, tenantId, Arg.Any<CancellationToken>())
            .Returns(owner);
        roleRepository.GetByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<UserTenantRole> { owner, coOwner });

        var sut = CreateSut(roleRepository, unitOfWork);

        var result = await sut.Handle(new AdminRemoveMemberCommand(tenantId, ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        roleRepository.Received(1).Remove(owner);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ordinary_member_is_removed_without_an_owner_check()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var client = UserTenantRole.Create(clientId, tenantId, TenantRole.Client);
        roleRepository.GetByUserAndTenantAsync(clientId, tenantId, Arg.Any<CancellationToken>())
            .Returns(client);

        var sut = CreateSut(roleRepository, unitOfWork);

        var result = await sut.Handle(new AdminRemoveMemberCommand(tenantId, clientId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        roleRepository.Received(1).Remove(client);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // A non-Owner removal must not even consult the roster for an owner count.
        await roleRepository.DidNotReceive().GetByTenantAsync(tenantId, Arg.Any<CancellationToken>());
    }
}
