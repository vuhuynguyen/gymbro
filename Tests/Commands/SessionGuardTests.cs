using BuildingBlocks.Shared.Abstractions;
using Modules.WorkoutSessionModule.Application;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The shared session access/state guard. <c>LoadOwnedInProgressAsync</c> gates the live-session flow
/// (complete/abandon) to InProgress; <c>LoadOwnedEditableAsync</c> additionally allows a COMPLETED session
/// so a finished workout can be corrected in place (fix/add sets, add exercises). Both share the same
/// 404-if-missing / 403-if-not-owner contract; only an ABANDONED session is non-editable.
/// </summary>
public sealed class SessionGuardTests
{
    private static (IWorkoutSessionRepository repo, ICurrentUser user) Mocks(
        WorkoutSession? session, Guid callerId)
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(session);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(callerId);
        return (repo, user);
    }

    private static WorkoutSession StartOwned(Guid traineeId) => WorkoutSession.Start(
        traineeId, Guid.NewGuid(), SessionSource.Adhoc, null, null, null, null, null, null);

    [Fact]
    public async Task Editable_allows_an_in_progress_session()
    {
        var trainee = Guid.NewGuid();
        var session = StartOwned(trainee);
        var (repo, user) = Mocks(session, trainee);

        var result = await SessionGuard.LoadOwnedEditableAsync(repo, user, session.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Editable_allows_a_completed_session()
    {
        var trainee = Guid.NewGuid();
        var session = StartOwned(trainee);
        session.Complete(null, null, null, prCount: 0);
        var (repo, user) = Mocks(session, trainee);

        var result = await SessionGuard.LoadOwnedEditableAsync(repo, user, session.Id, CancellationToken.None);

        Assert.True(result.IsSuccess); // edit-in-place: a finished workout is editable
    }

    [Fact]
    public async Task Editable_rejects_an_abandoned_session_with_conflict()
    {
        var trainee = Guid.NewGuid();
        var session = StartOwned(trainee);
        session.Abandon(null);
        var (repo, user) = Mocks(session, trainee);

        var result = await SessionGuard.LoadOwnedEditableAsync(repo, user, session.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Editable_rejects_another_trainees_session_with_unauthorized()
    {
        var session = StartOwned(Guid.NewGuid());
        var (repo, user) = Mocks(session, Guid.NewGuid()); // caller != owner

        var result = await SessionGuard.LoadOwnedEditableAsync(repo, user, session.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Editable_returns_not_found_for_a_missing_session()
    {
        var (repo, user) = Mocks(null, Guid.NewGuid());

        var result = await SessionGuard.LoadOwnedEditableAsync(repo, user, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [Fact]
    public async Task InProgress_guard_still_rejects_a_completed_session()
    {
        var trainee = Guid.NewGuid();
        var session = StartOwned(trainee);
        session.Complete(null, null, null, prCount: 0);
        var (repo, user) = Mocks(session, trainee);

        var result = await SessionGuard.LoadOwnedInProgressAsync(repo, user, session.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code); // complete/abandon stay in-progress-only
    }
}
