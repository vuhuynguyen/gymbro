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
/// "A trainee may add an ad-hoc performed exercise only to their own session while it is InProgress."
/// The handler enforces three guards in order — missing session → NotFound, wrong trainee → Unauthorized,
/// non-InProgress status → Conflict — and on the happy path captures the exercise name (via the cross-module
/// <see cref="ResolveExerciseNamesQuery"/>) so the log survives a later rename, persists via the repository
/// plus a single SaveChanges, and returns the mapped DTO. Fully mocked — no database.
/// </summary>
public sealed class AddPerformedExerciseHandlerTests
{
    private static AddPerformedExerciseHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        Guid tenantId,
        Guid currentUserId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        var currentUser = Substitute.For<ICurrentUser>();
        tenantContext.TenantId.Returns(tenantId);
        currentUser.UserId.Returns(currentUserId);

        return new AddPerformedExerciseHandler(
            sessionRepository,
            exerciseRepository,
            unitOfWork,
            tenantContext,
            currentUser,
            mediator);
    }

    private static AddPerformedExerciseCommand CreateCommand(Guid sessionId, Guid exerciseId)
        => new(sessionId, exerciseId, PlanWorkoutExerciseId: null, Order: 1, Notes: null);

    [Fact]
    public async Task Missing_session_returns_not_found_and_never_persists()
    {
        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, unitOfWork, mediator, Guid.NewGuid(), Guid.NewGuid());

        var result = await sut.Handle(
            CreateCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await exerciseRepository.DidNotReceive()
            .AddAsync(Arg.Any<PerformedExercise>(), Arg.Any<CancellationToken>());
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

        var session = WorkoutSession.Start(
            ownerId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, unitOfWork, mediator, tenantId, callerId);

        var result = await sut.Handle(
            CreateCommand(session.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);

        await exerciseRepository.DidNotReceive()
            .AddAsync(Arg.Any<PerformedExercise>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Adding_to_a_non_in_progress_session_returns_conflict()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        // Already terminal: no further exercises may be appended.
        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        session.Complete(null, null, null, prCount: 0);

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, unitOfWork, mediator, tenantId, traineeId);

        var result = await sut.Handle(
            CreateCommand(session.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        await exerciseRepository.DidNotReceive()
            .AddAsync(Arg.Any<PerformedExercise>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_adding_to_in_progress_adhoc_session_captures_name_and_persists()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mediator = Substitute.For<IMediator>();

        // Ad-hoc session (no assignment) → the DisableTraineeEditing guard short-circuits to false
        // without dispatching GetPlanAssignmentByIdQuery.
        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // The name is resolved at log time so a later rename/delete cannot relabel this entry.
        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>
        {
            [exerciseId] = "Back Squat"
        };
        mediator.Send(Arg.Any<ResolveExerciseNamesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, string>>.Success(names));
        IReadOnlyDictionary<Guid, ExerciseTrackingType> tracking = new Dictionary<Guid, ExerciseTrackingType>
        {
            [exerciseId] = ExerciseTrackingType.Strength
        };
        mediator.Send(Arg.Any<ResolveExerciseTrackingTypesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, ExerciseTrackingType>>.Success(tracking));

        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(
            sessionRepository, exerciseRepository, unitOfWork, mediator, tenantId, traineeId);

        var result = await sut.Handle(
            new AddPerformedExerciseCommand(session.Id, exerciseId, null, Order: 2, Notes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value!;
        Assert.Equal(exerciseId, value.ExerciseId);
        Assert.Equal("Back Squat", value.ExerciseName);
        Assert.Equal(2, value.Order);
        Assert.Equal(ExercisePerformStatus.InProgress, value.Status);

        // Persisted with the captured name + tenant, then committed exactly once.
        await exerciseRepository.Received(1).AddAsync(
            Arg.Is<PerformedExercise>(e =>
                e.SessionId == session.Id &&
                e.ExerciseId == exerciseId &&
                e.ExerciseName == "Back Squat" &&
                e.Order == 2),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
