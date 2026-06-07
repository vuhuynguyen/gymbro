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
/// "A trainee may delete a logged set only from their own in-progress session, and only when the set truly
/// belongs to the addressed exercise." The handler walks four guards in order — missing session → NotFound,
/// wrong trainee → Unauthorized, non-InProgress status → Conflict, and a set whose parent exercise does not
/// match the addressed exercise → NotFound — and on success removes the set and commits exactly once.
/// Fully mocked — no database.
/// </summary>
public sealed class DeleteSetHandlerTests
{
    private static DeleteSetHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        IPerformedSetRepository setRepository,
        IUnitOfWork unitOfWork,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new DeleteSetHandler(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, currentUser);
    }

    [Fact]
    public async Task Missing_session_returns_not_found_and_never_removes_or_persists()
    {
        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(
            new DeleteSetCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        setRepository.DidNotReceive().Remove(Arg.Any<PerformedSet>());
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
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            ownerId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, callerId);

        var result = await sut.Handle(
            new DeleteSetCommand(session.Id, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        setRepository.DidNotReceive().Remove(Arg.Any<PerformedSet>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Set_belonging_to_a_different_exercise_returns_not_found()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);

        var exercise = PerformedExercise.Create(
            session.Id, tenantId, Guid.NewGuid(), null, 1, "Bench Press");

        // The set is parented to some OTHER exercise, so it must not be deletable via this one.
        var foreignSet = PerformedSet.Log(
            Guid.NewGuid(), tenantId, null, 1, PerformedSetType.Working, 8, 60m, null, null, null, null, true);

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);
        setRepository.GetByIdAsync(foreignSet.Id, Arg.Any<CancellationToken>()).Returns(foreignSet);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new DeleteSetCommand(session.Id, exercise.Id, foreignSet.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        setRepository.DidNotReceive().Remove(Arg.Any<PerformedSet>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_deleting_an_owned_set_removes_it_and_persists_once()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);

        var exercise = PerformedExercise.Create(
            session.Id, tenantId, Guid.NewGuid(), null, 1, "Bench Press");

        var set = PerformedSet.Log(
            exercise.Id, tenantId, null, 1, PerformedSetType.Working, 8, 60m, null, null, null, null, true);

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);
        setRepository.GetByIdAsync(set.Id, Arg.Any<CancellationToken>()).Returns(set);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new DeleteSetCommand(session.Id, exercise.Id, set.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The matched set was removed and the work committed exactly once.
        setRepository.Received(1).Remove(set);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
