using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Commands.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// "A session can only be completed by its own trainee, exactly once, while it is InProgress."
/// The handler enforces three guards in order — missing session → NotFound, wrong trainee → Unauthorized,
/// non-InProgress status → Conflict — and on success marks the session Completed and persists via the unit
/// of work. Failure paths are fully mocked (no database); the success path backs the repositories' Query()
/// with EF InMemory so the handler's async LINQ (Include/ToListAsync/ToDictionaryAsync) translates.
/// </summary>
public sealed class CompleteSessionHandlerTests
{
    private static CompleteSessionHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IPerformedExerciseRepository exerciseRepository,
        IUnitOfWork unitOfWork,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new CompleteSessionHandler(
            sessionRepository, exerciseRepository, unitOfWork, currentUser);
    }

    [Fact]
    public async Task Missing_session_returns_not_found_and_never_persists()
    {
        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(
            new CompleteSessionCommand(Guid.NewGuid(), null, null, null), CancellationToken.None);

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

        var session = WorkoutSession.Start(
            ownerId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, callerId);

        var result = await sut.Handle(
            new CompleteSessionCommand(session.Id, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Completing_a_non_in_progress_session_returns_conflict()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // Already terminal: a second Complete must be rejected, not re-run.
        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        session.Complete(null, null, null, prCount: 0);

        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new CompleteSessionCommand(session.Id, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_completing_in_progress_session_marks_it_completed_and_persists()
    {
        var traineeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        await using var db = NewDb();

        var session = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        db.Set<WorkoutSession>().Add(session);
        await db.SaveChangesAsync();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var exerciseRepository = Substitute.For<IPerformedExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // Query() is backed by EF InMemory so the handler's Include/ToListAsync/ToDictionaryAsync translate.
        sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepository.Query().Returns(db.Set<WorkoutSession>());
        exerciseRepository.Query().Returns(db.Set<PerformedExercise>());
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(sessionRepository, exerciseRepository, unitOfWork, traineeId);

        var result = await sut.Handle(
            new CompleteSessionCommand(session.Id, RpeOverall: 8, Notes: "done", completedAt),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value!;
        Assert.Equal(session.Id, value.SessionId);
        Assert.Equal(0, value.TotalExercises);
        Assert.Equal(0, value.TotalSets);
        Assert.Equal(completedAt, value.CompletedAt);

        // Domain state transitioned and the work was committed exactly once.
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal(completedAt, session.CompletedAt);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static AppDbContext NewDb() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"complete-session-{Guid.NewGuid()}")
                .Options,
            new StubDbContextServices(),
            TestModelConfigurations.All());

    /// <summary>Minimal context services for an HTTP-less test: admin so EF global filters never exclude rows.</summary>
    private sealed class StubDbContextServices : IDbContextServices, ICurrentUser, ITenantContext
    {
        public ICurrentUser CurrentUser => this;
        public ITenantContext TenantContext => this;
        public Guid UserId => Guid.Empty;
        public bool IsAdmin => true;
        public string? TimeZoneId => null;
        public Guid? TenantId => null;
    }
}