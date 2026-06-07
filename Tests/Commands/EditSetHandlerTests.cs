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
/// "A set can only be edited by the session's own trainee, while the session is InProgress, and only when the
/// set actually belongs to the named exercise of that session." The handler enforces, in order: missing
/// session → NotFound, wrong trainee → Unauthorized, non-InProgress status → Conflict, then exercise/set
/// membership → NotFound. On success it mutates the set via <c>PerformedSet.Edit</c> and persists once.
/// Fully mocked — no database (every dependency is a repository / unit-of-work / current-user substitute).
/// </summary>
public sealed class EditSetHandlerTests
{
    private static EditSetHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        IPerformedSetRepository setRepository,
        IUnitOfWork unitOfWork,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new EditSetHandler(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, currentUser);
    }

    private static EditSetCommand CreateCommand(Guid sessionId, Guid exerciseId, Guid setId)
        => new(
            sessionId,
            exerciseId,
            setId,
            Reps: 8,
            WeightKg: 100m,
            DurationSeconds: null,
            DistanceM: null,
            Rpe: 9,
            RestSeconds: 90,
            IsCompleted: true,
            SetType: PerformedSetType.Working);

    [Fact]
    public async Task Missing_session_returns_not_found_and_never_persists()
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
            CreateCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
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
            CreateCommand(session.Id, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);

        // Rejected before even loading the exercise/set or persisting.
        await exerciseRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Editing_a_set_on_a_non_in_progress_session_returns_conflict()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // Already terminal: edits must be rejected once the session is no longer InProgress.
        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        session.Complete(null, null, null, prCount: 0);

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            CreateCommand(session.Id, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_editing_a_matching_set_mutates_it_and_persists_once()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);

        // Exercise belongs to the session; set belongs to the exercise → both membership checks pass.
        var exercise = PerformedExercise.Create(session.Id, tenantId, Guid.NewGuid(), null, 0, "Squat");
        var set = PerformedSet.Log(
            exercise.Id, tenantId, null, setNumber: 1, PerformedSetType.Working,
            reps: 5, weightKg: 60m, durationSeconds: null, distanceM: null,
            rpe: null, restSeconds: null, isCompleted: false);

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);
        setRepository.GetByIdAsync(set.Id, Arg.Any<CancellationToken>()).Returns(set);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            CreateCommand(session.Id, exercise.Id, set.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Domain state was mutated from the command's new values, and committed exactly once.
        Assert.Equal(8, set.Reps);
        Assert.Equal(100m, set.WeightKg);
        Assert.Equal(9, set.Rpe);
        Assert.Equal(90, set.RestSeconds);
        Assert.True(set.IsCompleted);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
