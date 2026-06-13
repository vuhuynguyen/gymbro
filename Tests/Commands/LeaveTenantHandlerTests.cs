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
/// Pins the "membership leave" rules: a non-member gets NotFound; the sole remaining Owner is blocked from
/// leaving (Validation) so a tenant is never orphaned, while a co-Owner and an ordinary member may leave —
/// their role row is removed and persisted. Fully mocked — no database.
/// </summary>
public sealed class LeaveTenantHandlerTests
{
    private static LeaveTenantHandler CreateSut(
        IUserTenantRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        Guid userId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);

        // The handler runs its logic inside unitOfWork.ExecuteTransactionalAsync; invoke the action inline so
        // these mocked-repository tests exercise it (the real per-tenant advisory lock is integration-level).
        unitOfWork.ExecuteTransactionalAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<Task>)ci[0]).Invoke());

        return new LeaveTenantHandler(roleRepository, currentUser, unitOfWork);
    }

    [Fact]
    public async Task Non_member_gets_not_found_and_nothing_is_persisted()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // The caller has no membership in this tenant.
        roleRepository.GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns((UserTenantRole?)null);

        var sut = CreateSut(roleRepository, unitOfWork, userId);

        var result = await sut.Handle(new LeaveTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        roleRepository.DidNotReceive().Remove(Arg.Any<UserTenantRole>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sole_owner_cannot_leave_and_is_rejected_with_validation()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var membership = UserTenantRole.Create(userId, tenantId, TenantRole.Owner);
        roleRepository.GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns(membership);

        // The only Owner in the tenant (plus an unrelated Client) → leaving would orphan ownership.
        var client = UserTenantRole.Create(Guid.NewGuid(), tenantId, TenantRole.Client);
        roleRepository.GetByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<UserTenantRole> { membership, client });

        var sut = CreateSut(roleRepository, unitOfWork, userId);

        var result = await sut.Handle(new LeaveTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);
        Assert.Contains("only owner", result.Error.Message, StringComparison.OrdinalIgnoreCase);

        roleRepository.DidNotReceive().Remove(Arg.Any<UserTenantRole>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_with_another_owner_present_can_leave()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var membership = UserTenantRole.Create(userId, tenantId, TenantRole.Owner);
        roleRepository.GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns(membership);

        // A second Owner exists → ownership is not orphaned, so the caller may leave.
        var otherOwner = UserTenantRole.Create(Guid.NewGuid(), tenantId, TenantRole.Owner);
        roleRepository.GetByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<UserTenantRole> { membership, otherOwner });

        var sut = CreateSut(roleRepository, unitOfWork, userId);

        var result = await sut.Handle(new LeaveTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The membership change is serialised by the per-tenant advisory lock before the owner-count check.
        await roleRepository.Received(1).LockForTenantMembershipChangeAsync(tenantId, Arg.Any<CancellationToken>());
        roleRepository.Received(1).Remove(membership);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ordinary_member_leaves_and_role_is_removed_then_saved()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // A non-Owner member: the owner-count check is skipped entirely.
        var membership = UserTenantRole.Create(userId, tenantId, TenantRole.Client);
        roleRepository.GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns(membership);

        var sut = CreateSut(roleRepository, unitOfWork, userId);

        var result = await sut.Handle(new LeaveTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Non-owner path does not consult the tenant roster.
        await roleRepository.DidNotReceive().GetByTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        roleRepository.Received(1).Remove(membership);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
