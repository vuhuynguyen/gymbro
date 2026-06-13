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
/// Joining a tenant via an invite code enforces three guards in order — an unknown/expired code is
/// <c>NotFound</c>, a caller with no user profile is <c>NotFound</c>, and a caller who is already a member of
/// the invite's tenant is <c>Conflict</c> — and on success it atomically claims the single-use invite, creates
/// the membership row via the repository, and commits exactly once, returning the joined tenant id. A redemption
/// that loses the race to claim the invite is <c>Conflict</c>. Fully mocked — no database.
/// </summary>
public sealed class JoinTenantHandlerTests
{
    private static JoinTenantHandler CreateSut(
        IInviteRepository inviteRepository,
        IUserTenantRoleRepository roleRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new JoinTenantHandler(
            inviteRepository, roleRepository, userRepository, currentUser, unitOfWork);
    }

    private static Invite CreateInvite(Guid tenantId)
        => Invite.Create("trainee@example.com", tenantId, TenantRole.Client, DateTimeOffset.UtcNow.AddDays(7));

    [Fact]
    public async Task Unknown_or_expired_code_returns_not_found_and_never_persists()
    {
        var inviteRepository = Substitute.For<IInviteRepository>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // No active invite matches the code → short-circuits before anything else.
        inviteRepository.GetActiveByCodeAsync("BADCODE", Arg.Any<CancellationToken>())
            .Returns((Invite?)null);

        var sut = CreateSut(inviteRepository, roleRepository, userRepository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(new JoinTenantCommand("BADCODE"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await roleRepository.DidNotReceive().AddAsync(Arg.Any<UserTenantRole>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caller_without_user_profile_returns_not_found()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        inviteRepository.GetActiveByCodeAsync("GOODCODE", Arg.Any<CancellationToken>())
            .Returns(CreateInvite(tenantId));
        // The authenticated caller has no User aggregate yet.
        userRepository.GetByIdAsync(currentUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var sut = CreateSut(inviteRepository, roleRepository, userRepository, unitOfWork, currentUserId);

        var result = await sut.Handle(new JoinTenantCommand("GOODCODE"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await roleRepository.DidNotReceive().AddAsync(Arg.Any<UserTenantRole>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caller_already_a_member_of_the_tenant_is_conflict()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var invite = CreateInvite(tenantId);
        inviteRepository.GetActiveByCodeAsync("GOODCODE", Arg.Any<CancellationToken>()).Returns(invite);
        userRepository.GetByIdAsync(currentUserId, Arg.Any<CancellationToken>())
            .Returns(User.Create(currentUserId, "Existing Member"));
        // The caller already holds a role in this tenant → duplicate membership.
        roleRepository.GetByUserAndTenantAsync(currentUserId, tenantId, Arg.Any<CancellationToken>())
            .Returns(UserTenantRole.Create(currentUserId, tenantId, TenantRole.Client));

        var sut = CreateSut(inviteRepository, roleRepository, userRepository, unitOfWork, currentUserId);

        var result = await sut.Handle(new JoinTenantCommand("GOODCODE"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        // Rejected before claiming the invite or persisting anything.
        await inviteRepository.DidNotReceive().TryClaimAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await roleRepository.DidNotReceive().AddAsync(Arg.Any<UserTenantRole>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Losing_the_race_to_claim_the_invite_is_conflict()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var invite = CreateInvite(tenantId);
        inviteRepository.GetActiveByCodeAsync("GOODCODE", Arg.Any<CancellationToken>()).Returns(invite);
        userRepository.GetByIdAsync(currentUserId, Arg.Any<CancellationToken>())
            .Returns(User.Create(currentUserId, "New Member"));
        roleRepository.GetByUserAndTenantAsync(currentUserId, tenantId, Arg.Any<CancellationToken>())
            .Returns((UserTenantRole?)null);
        // Another redemption already consumed this single-use invite → the atomic claim affects 0 rows.
        inviteRepository.TryClaimAsync(invite.Id, Arg.Any<CancellationToken>()).Returns(false);

        var sut = CreateSut(inviteRepository, roleRepository, userRepository, unitOfWork, currentUserId);

        var result = await sut.Handle(new JoinTenantCommand("GOODCODE"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        // No membership is granted when the invite could not be claimed.
        await roleRepository.DidNotReceive().AddAsync(Arg.Any<UserTenantRole>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_join_claims_invite_adds_role_and_returns_tenant_id()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var inviteRepository = Substitute.For<IInviteRepository>();
        var roleRepository = Substitute.For<IUserTenantRoleRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var invite = CreateInvite(tenantId);
        inviteRepository.GetActiveByCodeAsync("GOODCODE", Arg.Any<CancellationToken>()).Returns(invite);
        userRepository.GetByIdAsync(currentUserId, Arg.Any<CancellationToken>())
            .Returns(User.Create(currentUserId, "New Member"));
        // Not yet a member → join proceeds.
        roleRepository.GetByUserAndTenantAsync(currentUserId, tenantId, Arg.Any<CancellationToken>())
            .Returns((UserTenantRole?)null);
        // This redemption wins the atomic claim.
        inviteRepository.TryClaimAsync(invite.Id, Arg.Any<CancellationToken>()).Returns(true);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(inviteRepository, roleRepository, userRepository, unitOfWork, currentUserId);

        var result = await sut.Handle(new JoinTenantCommand("GOODCODE"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value!;
        Assert.Equal(tenantId, value);

        // Side effects: invite claimed atomically, membership row created with the invite's role, single commit.
        await inviteRepository.Received(1).TryClaimAsync(invite.Id, Arg.Any<CancellationToken>());
        await roleRepository.Received(1).AddAsync(
            Arg.Is<UserTenantRole>(r =>
                r.UserId == currentUserId &&
                r.TenantId == tenantId &&
                r.Role == invite.Role),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
