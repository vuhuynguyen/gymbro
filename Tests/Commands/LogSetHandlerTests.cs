using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Tracking;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Commands.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// "A set may only be logged into your own in-progress session, against an exercise that belongs to that
/// session." The handler enforces the guards in order — missing session → NotFound, wrong trainee →
/// Unauthorized, non-InProgress status → Conflict, and an exercise that is missing or belongs to a different
/// session → NotFound — and on the happy path persists the logged set via the repository plus a single
/// SaveChanges, returning a DTO that reflects the command. Fully mocked — no database.
/// </summary>
public sealed class LogSetHandlerTests
{
    private static LogSetHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        IPerformedSetRepository setRepository,
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid currentUserId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        var currentUser = Substitute.For<ICurrentUser>();
        tenantContext.TenantId.Returns(tenantId);
        currentUser.UserId.Returns(currentUserId);

        return new LogSetHandler(
            sessionRepository,
            exerciseRepository,
            setRepository,
            unitOfWork,
            tenantContext,
            currentUser);
    }

    private static LogSetCommand CreateCommand(Guid sessionId, Guid exerciseId)
        => new(
            sessionId,
            exerciseId,
            PlanSetId: null,
            SetNumber: 1,
            SetType: PerformedSetType.Working,
            Reps: 5,
            WeightKg: 100m,
            DurationSeconds: null,
            DistanceM: null,
            Rpe: 8,
            RestSeconds: 120,
            IsCompleted: true);

    [Fact]
    public async Task Missing_session_returns_not_found_and_never_persists()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        var result = await sut.Handle(
            CreateCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await setRepository.DidNotReceive().AddAsync(Arg.Any<PerformedSet>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Session_owned_by_another_trainee_is_unauthorized()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            ownerId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, callerId);

        var result = await sut.Handle(
            CreateCommand(session.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);

        await setRepository.DidNotReceive().AddAsync(Arg.Any<PerformedSet>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exercise_belonging_to_another_session_returns_not_found()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Exercise exists but is anchored to a different session id → must be rejected as NotFound.
        var foreignExercise = PerformedExercise.Create(
            Guid.NewGuid(), tenantId, Guid.NewGuid(), null, 0, "Squat");
        exerciseRepository.GetByIdAsync(foreignExercise.Id, Arg.Any<CancellationToken>())
            .Returns(foreignExercise);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        var result = await sut.Handle(
            CreateCommand(session.Id, foreignExercise.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await setRepository.DidNotReceive().AddAsync(Arg.Any<PerformedSet>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_logging_into_in_progress_session_persists_set_and_returns_dto()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var exercise = PerformedExercise.Create(
            session.Id, tenantId, Guid.NewGuid(), null, 0, "Squat");
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>())
            .Returns(exercise);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        var result = await sut.Handle(
            CreateCommand(session.Id, exercise.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value!;
        Assert.Equal(1, value.SetNumber);
        Assert.Equal(PerformedSetType.Working, value.SetType);
        Assert.Equal(5, value.Reps);
        Assert.Equal(100m, value.WeightKg);
        Assert.True(value.IsCompleted);

        // Persisted once: a set built for this exercise + tenant, then a single SaveChanges.
        await setRepository.Received(1).AddAsync(
            Arg.Is<PerformedSet>(s =>
                s.PerformedExerciseId == exercise.Id &&
                s.SetNumber == 1 &&
                s.SetType == PerformedSetType.Working &&
                s.Reps == 5 &&
                s.WeightKg == 100m &&
                s.IsCompleted),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rest_taken_is_persisted_on_the_logged_set()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var exercise = PerformedExercise.Create(session.Id, tenantId, Guid.NewGuid(), null, 0, "Squat");
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        // CreateCommand sets RestSeconds: 120 — assert it reaches the persisted set.
        var result = await sut.Handle(CreateCommand(session.Id, exercise.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await setRepository.Received(1).AddAsync(
            Arg.Is<PerformedSet>(s => s.RestSeconds == 120 && s.ParentSetId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Drop_stage_with_parent_in_another_exercise_is_not_found()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var exercise = PerformedExercise.Create(session.Id, tenantId, Guid.NewGuid(), null, 0, "Squat");
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);

        // The parent set belongs to a DIFFERENT performed exercise → must be rejected.
        var foreignParent = PerformedSet.Log(Guid.NewGuid(), tenantId, null, 1, PerformedSetType.Working, 6, 100m, null, null, null, null, true);
        setRepository.GetByIdAsync(foreignParent.Id, Arg.Any<CancellationToken>()).Returns(foreignParent);

        var sut = CreateSut(sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        var command = new LogSetCommand(
            session.Id, exercise.Id, PlanSetId: null, SetNumber: 2, SetType: PerformedSetType.Drop,
            Reps: 4, WeightKg: 100m, DurationSeconds: null, DistanceM: null, Rpe: null, RestSeconds: null,
            IsCompleted: true, ParentSetId: foreignParent.Id);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await setRepository.DidNotReceive().AddAsync(Arg.Any<PerformedSet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Drop_stage_of_a_drop_stage_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var exercise = PerformedExercise.Create(session.Id, tenantId, Guid.NewGuid(), null, 0, "Squat");
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);

        // The "parent" is itself a drop stage (has a ParentSetId) → one level only, reject.
        var lead = PerformedSet.Log(exercise.Id, tenantId, null, 1, PerformedSetType.Working, 6, 100m, null, null, null, null, true);
        var stage = PerformedSet.Log(exercise.Id, tenantId, null, 2, PerformedSetType.Drop, 4, 100m, null, null, null, null, true, parentSetId: lead.Id);
        setRepository.GetByIdAsync(stage.Id, Arg.Any<CancellationToken>()).Returns(stage);

        var sut = CreateSut(sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        var command = new LogSetCommand(
            session.Id, exercise.Id, PlanSetId: null, SetNumber: 3, SetType: PerformedSetType.Drop,
            Reps: 3, WeightKg: 100m, DurationSeconds: null, DistanceM: null, Rpe: null, RestSeconds: null,
            IsCompleted: true, ParentSetId: stage.Id);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task Cardio_set_with_duration_only_is_logged()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        // A cardio exercise: duration is the primary metric, reps/weight are irrelevant.
        var exercise = PerformedExercise.Create(
            session.Id, tenantId, Guid.NewGuid(), null, 0, "Treadmill Run", ExerciseTrackingType.Cardio);
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        var command = new LogSetCommand(
            session.Id, exercise.Id, PlanSetId: null, SetNumber: 1, SetType: PerformedSetType.Working,
            Reps: null, WeightKg: null, DurationSeconds: 1200, DistanceM: 3000,
            Rpe: 7, RestSeconds: null, IsCompleted: true,
            Calories: 250, AvgHeartRate: 150, Rounds: null);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await setRepository.Received(1).AddAsync(
            Arg.Is<PerformedSet>(s => s.DurationSeconds == 1200 && s.DistanceM == 3000 && s.Calories == 250 && s.AvgHeartRate == 150 && s.Reps == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Strength_set_without_reps_is_rejected_as_validation()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var setRepository = Substitute.For<IPerformedSetRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        // Strength exercise (default mode): a set with no reps carries no primary metric.
        var exercise = PerformedExercise.Create(
            session.Id, tenantId, Guid.NewGuid(), null, 0, "Squat");
        exerciseRepository.GetByIdAsync(exercise.Id, Arg.Any<CancellationToken>()).Returns(exercise);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, setRepository, unitOfWork, tenantId, traineeId);

        var command = new LogSetCommand(
            session.Id, exercise.Id, PlanSetId: null, SetNumber: 1, SetType: PerformedSetType.Working,
            Reps: null, WeightKg: null, DurationSeconds: null, DistanceM: null,
            Rpe: null, RestSeconds: null, IsCompleted: true);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);
        await setRepository.DidNotReceive().AddAsync(Arg.Any<PerformedSet>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
