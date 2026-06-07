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
/// Pins tenant creation: an unauthenticated caller (empty <c>UserId</c>) is rejected up front with the literal
/// <c>Unauthorized</c> code before any persistence, and an authenticated caller becomes the tenant <see
/// cref="TenantRole.Owner"/> — the handler adds the tenant, adds the owner role bound to that same caller and
/// the new tenant, and commits exactly once, returning the new tenant id. Fully mocked — no database.
/// </summary>
public sealed class CreateTenantHandlerTests
{
    private static CreateTenantHandler CreateSut(
        ITenantRepository tenantRepository,
        IUserTenantRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new CreateTenantHandler(
            tenantRepository, roleRepository, currentUser, unitOfWork);
    }

    [Fact]
    public async Task Unauthenticated_caller_is_rejected_and_never_persists()
    {
        var tenantRepository = Substitute.For<ITenantRepository>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // No authenticated user → short-circuit before any write.
        var sut = CreateSut(tenantRepository, roleRepository, unitOfWork, Guid.Empty);

        var result = await sut.Handle(new CreateTenantCommand("Iron House"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);

        await tenantRepository.DidNotReceive().AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await roleRepository.DidNotReceive().AddAsync(Arg.Any<UserTenantRole>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticated_caller_creates_tenant_and_becomes_owner_and_saves_once()
    {
        var ownerId = Guid.NewGuid();

        var tenantRepository = Substitute.For<ITenantRepository>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        Tenant? added = null;
        await tenantRepository.AddAsync(Arg.Do<Tenant>(t => added = t), Arg.Any<CancellationToken>());

        var sut = CreateSut(tenantRepository, roleRepository, unitOfWork, ownerId);

        var result = await sut.Handle(new CreateTenantCommand("Iron House"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var tenantId = result.Value;
        Assert.NotEqual(Guid.Empty, tenantId);

        // The tenant was built from the command and owned by the caller; its id is what the handler returned.
        Assert.NotNull(added);
        var tenant = added!;
        Assert.Equal(tenantId, tenant.Id);
        Assert.Equal("Iron House", tenant.Name);
        Assert.Equal(ownerId, tenant.OwnerUserId);

        // The caller is granted the Owner role on exactly this new tenant.
        await roleRepository.Received(1).AddAsync(
            Arg.Is<UserTenantRole>(r =>
                r.UserId == ownerId &&
                r.TenantId == tenantId &&
                r.Role == TenantRole.Owner),
            Arg.Any<CancellationToken>());

        // Tenant insert + a single commit.
        await tenantRepository.Received(1).AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
