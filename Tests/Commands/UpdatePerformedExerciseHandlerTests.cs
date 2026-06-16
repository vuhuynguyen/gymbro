using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Commands.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the guards on editing a logged exercise mid-session. The handler rejects, in order: an unknown
/// session (NotFound), a session owned by another trainee (Unauthorized), and a session that is not
/// InProgress (Conflict). The happy path — substituting an exercise on the trainee's own ad-hoc,
/// in-progress session — resolves the substitute's display name via <see cref="IMediator"/>, mutates the
/// entity to Substituted, and commits exactly once. Fully mocked — no database.
/// </summary>
public sealed class UpdatePerformedExerciseHandlerTests
{
    private static UpdatePerformedExerciseHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new UpdatePerformedExerciseHandler(
            sessionRepository, exerciseRepository, unitOfWork, currentUser, mediator);
    }

    private static WorkoutSession StartAdhocSession(Guid traineeId, Guid tenantId)
        => WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);

    [Fact]
    public async Task Missing_session_returns_not_found_and_never_persists()
    {
        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, mediator, Guid.NewGuid());

        var result = await sut.Handle(
            new UpdatePerformedExerciseCommand(
                Guid.NewGuid(), Guid.NewGuid(), ExerciseUpdateAction.Skip, null, null),
            CancellationToken.None);

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
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        var session = StartAdhocSession(ownerId, tenantId);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, mediator, callerId);

        var result = await sut.Handle(
            new UpdatePerformedExerciseCommand(
                session.Id, Guid.NewGuid(), ExerciseUpdateAction.Skip, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updating_an_abandoned_session_returns_conflict()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        // Abandoned stays read-only (a COMPLETED session is now editable in place; an abandoned one isn't).
        var session = StartAdhocSession(traineeId, tenantId);
        session.Abandon(null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, mediator, traineeId);

        var result = await sut.Handle(
            new UpdatePerformedExerciseCommand(
                session.Id, Guid.NewGuid(), ExerciseUpdateAction.Skip, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_substituting_exercise_captures_name_marks_substituted_and_persists()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var substituteExerciseId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        // Ad-hoc, in-progress session owned by the caller → no DisableTraineeEditing assignment to check.
        var session = StartAdhocSession(traineeId, tenantId);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var exercise = PerformedExercise.Create(
            session.Id, tenantId, Guid.NewGuid(), null, order: 1, exerciseName: "Bench Press");
        exerciseRepository.GetByIdWithSetsAsync(exercise.Id, Arg.Any<CancellationToken>())
            .Returns(exercise);

        // The handler resolves the substitute's display name for durable history.
        IReadOnlyDictionary<Guid, string> names =
            new Dictionary<Guid, string> { [substituteExerciseId] = "Dumbbell Press" };
        mediator.Send(Arg.Any<ResolveExerciseNamesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, string>>.Success(names));
        IReadOnlyDictionary<Guid, ExerciseTrackingType> tracking =
            new Dictionary<Guid, ExerciseTrackingType> { [substituteExerciseId] = ExerciseTrackingType.Strength };
        mediator.Send(Arg.Any<ResolveExerciseTrackingTypesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>.Success(tracking));

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, mediator, traineeId);

        var result = await sut.Handle(
            new UpdatePerformedExerciseCommand(
                session.Id, exercise.Id, ExerciseUpdateAction.Substitute, substituteExerciseId, "swap"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Entity mutated to the substitute, with the resolved name captured, then committed once.
        Assert.Equal(ExercisePerformStatus.Substituted, exercise.Status);
        Assert.Equal(substituteExerciseId, exercise.ExerciseId);
        Assert.Equal("Dumbbell Press", exercise.ExerciseName);
        Assert.Equal("swap", exercise.Notes);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
