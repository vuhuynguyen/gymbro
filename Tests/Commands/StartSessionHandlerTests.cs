using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Commands.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// "At most one in-progress session per trainee per tenant." The handler rejects a second start two ways:
/// the pre-check short-circuits the common case, and a <see cref="DbUpdateException"/> backstop maps the
/// concurrent-race unique-index violation to a clean Conflict (409) instead of letting it surface as a 500.
/// Fully mocked — no database.
/// </summary>
public sealed class StartSessionHandlerTests
{
    private static StartSessionHandler CreateSut(
        IWorkoutSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid traineeId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        var currentUser = Substitute.For<ICurrentUser>();
        tenantContext.TenantId.Returns(tenantId);
        currentUser.UserId.Returns(traineeId);

        return new StartSessionHandler(
            sessionRepository, Substitute.For<IMediator>(), unitOfWork, tenantContext, currentUser);
    }

    [Fact]
    public async Task Trainee_with_active_session_gets_conflict_before_any_insert()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // The trainee already has an in-progress session → pre-check short-circuits.
        var existing = WorkoutSession.Start(
            traineeId, tenantId, SessionSource.Adhoc, null, null, null, null, null, null);
        sessionRepository.GetActiveForTraineeAsync(traineeId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var sut = CreateSut(sessionRepository, unitOfWork, tenantId, traineeId);

        var result = await sut.Handle(
            new StartSessionCommand(SessionSource.Adhoc, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        // Rejected before touching persistence.
        await sessionRepository.DidNotReceive().AddAsync(Arg.Any<WorkoutSession>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_start_violating_unique_index_maps_to_conflict()
    {
        var tenantId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var sessionRepository = Substitute.For<IWorkoutSessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // Pre-check passes (no active session yet), but a concurrent request wins the race: the partial
        // unique index rejects this insert with a DbUpdateException at SaveChanges.
        sessionRepository.GetActiveForTraineeAsync(traineeId, Arg.Any<CancellationToken>())
            .Returns((WorkoutSession?)null);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException());

        var sut = CreateSut(sessionRepository, unitOfWork, tenantId, traineeId);

        var result = await sut.Handle(
            new StartSessionCommand(SessionSource.Adhoc, null, null, null, null), CancellationToken.None);

        // Mapped to Conflict (409) rather than bubbling up as an unhandled 500.
        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }
}
