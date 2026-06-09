using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Commands.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// "A trainee may fully remove an exercise only from their own in-progress session, and only when the
/// exercise truly belongs to that session." The handler walks four guards in order — missing session →
/// NotFound, wrong trainee → Unauthorized, non-InProgress status → Conflict, and an exercise whose
/// SessionId does not match the addressed session → NotFound — and on success removes the exercise
/// (its logged sets go too, by FK cascade) and commits exactly once. Sessions here are ad-hoc, so the
/// DisableTraineeEditing guard short-circuits without touching the mediator. Fully mocked — no database.
/// </summary>
public sealed class DeletePerformedExerciseHandlerTests
{
    private static DeletePerformedExerciseHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        IUnitOfWork unitOfWork,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new DeletePerformedExerciseHandler(
            sessionRepository, exerciseRepository, unitOfWork, currentUser, Substitute.For<IMediator>());
    }

    [Fact]
    public async Task Missing_session_returns_not_found_and_never_removes_or_persists()
    {
        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(
            new DeletePerformedExerciseCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        exerciseRepository.DidNotReceive().Remove(Arg.Any<PerformedExercise>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Session_owned_by_another_trainee_is_unauthorized()
    {
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            ownerId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, callerId);

        var result = await sut.Handle(
            new DeletePerformedExerciseCommand(session.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        exerciseRepository.DidNotReceive().Remove(Arg.Any<PerformedExercise>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_in_progress_session_returns_conflict()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        session.Abandon(null); // terminal — no longer editable

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new DeletePerformedExerciseCommand(session.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        exerciseRepository.DidNotReceive().Remove(Arg.Any<PerformedExercise>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exercise_belonging_to_a_different_session_returns_not_found()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);

        // Exercise parented to some OTHER session, so it must not be deletable via this one.
        var foreignExercise = PerformedExercise.Create(
            Guid.NewGuid(), tenantId, Guid.NewGuid(), null, 1, "Bench Press");

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        exerciseRepository.GetByIdAsync(foreignExercise.Id, Arg.Any<CancellationToken>()).Returns(foreignExercise);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new DeletePerformedExerciseCommand(session.Id, foreignExercise.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        exerciseRepository.DidNotReceive().Remove(Arg.Any<PerformedExercise>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_removing_an_owned_exercise_removes_it_and_persists_once()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);

        var exercise = PerformedExercise.Create(
            session.Id, tenantId, Guid.NewGuid(), null, 1, "Bench Press");

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new DeletePerformedExerciseCommand(session.Id, exercise.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        exerciseRepository.Received(1).Remove(exercise);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
