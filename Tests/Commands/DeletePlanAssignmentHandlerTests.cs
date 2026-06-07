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
/// Pins the delete-assignment behavior: a missing assignment is rejected with the literal
/// <c>NotFound</c> code and never touches persistence, while a found assignment is removed via the
/// repository and committed with a single SaveChanges. Fully mocked — no database.
/// </summary>
public sealed class DeletePlanAssignmentHandlerTests
{
    private static DeletePlanAssignmentHandler CreateSut(
        IPlanAssignmentRepository assignmentRepository,
        IUnitOfWork unitOfWork)
        => new(assignmentRepository, unitOfWork);

    private static PlanAssignment CreateAssignment(Guid tenantId, Guid createdBy, Guid traineeId, Guid planId)
        => PlanAssignment.Create(
            tenantId,
            createdBy,
            traineeId,
            planId,
            planVersion: 1,
            new DateOnly(2026, 6, 1),
            frequencyDaysPerWeek: 3,
            PlanVisibilityMode.Full,
            hideExercises: false,
            hideSetsReps: false,
            hideFutureWorkouts: false,
            disableTraineeEditing: false,
            snapshotJson: null);

    [Fact]
    public async Task Missing_assignment_returns_not_found_and_never_persists()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        assignmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PlanAssignment?)null);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new DeletePlanAssignmentCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        // Rejected before any persistence work.
        assignmentRepository.DidNotReceive().Remove(Arg.Any<PlanAssignment>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_assignment_is_removed_and_committed_once()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = CreateAssignment(tenantId, createdBy, traineeId, planId);
        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>())
            .Returns(assignment);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new DeletePlanAssignmentCommand(assignment.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value;
        Assert.True(value);

        // The exact assignment was removed, then a single SaveChanges committed the work.
        assignmentRepository.Received(1).Remove(assignment);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
