using BuildingBlocks.Application.Abstractions;
using Modules.WorkoutPlanModule.Application;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands.Handlers;
using Modules.WorkoutPlanModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the pause/resume rule for a plan assignment: pausing flips <see cref="PlanAssignment.IsActive"/> to
/// false (kept in history, hidden from the trainee's picker) and resuming flips it back to true, with the
/// change persisted via SaveChanges. A missing assignment maps to a clean NotFound rather than throwing.
/// Fully mocked — no database.
/// </summary>
public sealed class SetPlanAssignmentActiveHandlerTests
{
    private static SetPlanAssignmentActiveHandler CreateSut(
        IPlanAssignmentRepository assignmentRepository,
        IUnitOfWork unitOfWork)
    {
        return new SetPlanAssignmentActiveHandler(assignmentRepository, unitOfWork);
    }

    private static PlanAssignment BuildAssignment()
    {
        // Factory seeds IsActive = true; the handler mutates from there.
        return PlanAssignment.Create(
            tenantId: Guid.NewGuid(),
            createdBy: Guid.NewGuid(),
            traineeId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            planVersion: 1,
            startDate: new DateOnly(2026, 1, 1),
            frequencyDaysPerWeek: 3,
            visibilityMode: PlanVisibilityMode.Full,
            hideExercises: false,
            hideSetsReps: false,
            hideFutureWorkouts: false,
            disableTraineeEditing: false,
            snapshotJson: null);
    }

    [Fact]
    public async Task Pausing_sets_inactive_and_saves()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = BuildAssignment();
        Assert.True(assignment.IsActive); // sanity: starts active

        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>())
            .Returns(assignment);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new SetPlanAssignmentActiveCommand(assignment.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(assignment.IsActive); // paused
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resuming_sets_active_and_saves()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = BuildAssignment();
        assignment.SetActive(false); // start from a paused assignment

        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>())
            .Returns(assignment);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new SetPlanAssignmentActiveCommand(assignment.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(assignment.IsActive); // resumed
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_assignment_returns_not_found_without_saving()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        assignmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PlanAssignment?)null);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new SetPlanAssignmentActiveCommand(Guid.NewGuid(), false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        // Nothing persisted when the assignment does not exist.
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
