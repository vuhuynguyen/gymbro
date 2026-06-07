using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Commands.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the abandon lifecycle rule: a session can be abandoned only by its own trainee and only while
/// it is <see cref="SessionStatus.InProgress"/>. The handler enforces, in order, existence (NotFound),
/// ownership (Unauthorized), and the InProgress precondition (Conflict); on success it transitions the
/// session to <see cref="SessionStatus.Abandoned"/> and persists via SaveChanges. Fully mocked — no database.
/// </summary>
public sealed class AbandonSessionHandlerTests
{
    private static AbandonSessionHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        Guid callerId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(callerId);

        return new AbandonSessionHandler(sessionRepository, unitOfWork, currentUser);
    }

    private static WorkoutSession InProgressSession(Guid traineeId, Guid tenantId) =>
        WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);

    [Fact]
    public async Task Missing_session_returns_not_found_and_does_not_persist()
    {
        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);

        var sut = CreateSut(sessionRepository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(
            new AbandonSessionCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Session_owned_by_another_trainee_returns_unauthorized()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = InProgressSession(ownerId, tenantId);
        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(sessionRepository, unitOfWork, callerId);

        var result = await sut.Handle(
            new AbandonSessionCommand(session.Id, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        Assert.Equal(SessionStatus.InProgress, session.Status);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_in_progress_session_returns_conflict()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // Already terminal: completing it first moves it out of InProgress so abandon must be rejected.
        var session = InProgressSession(traineeId, tenantId);
        session.Complete(null, null, null, 0);
        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(sessionRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new AbandonSessionCommand(session.Id, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        Assert.Equal(SessionStatus.Completed, session.Status);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_abandoning_in_progress_session_transitions_to_abandoned_and_persists()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = InProgressSession(traineeId, tenantId);
        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(sessionRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new AbandonSessionCommand(session.Id, "gave up"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.Abandoned, session.Status);
        Assert.Equal("gave up", session.Notes);
        Assert.NotNull(session.CompletedAt);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
