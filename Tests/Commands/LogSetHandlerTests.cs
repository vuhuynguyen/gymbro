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
}
