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
/// Pins the update-assignment guards: a missing assignment is rejected with the literal <c>NotFound</c> code
/// and never touches persistence, while a valid request mutates the assignment's configuration in place and
/// commits via a single SaveChanges. Fully mocked — no database.
/// </summary>
public sealed class UpdatePlanAssignmentHandlerTests
{
    private static UpdatePlanAssignmentHandler CreateSut(
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
            startDate: new DateOnly(2026, 5, 1),
            frequencyDaysPerWeek: 3,
            visibilityMode: PlanVisibilityMode.Full,
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
            new UpdatePlanAssignmentCommand(
                AssignmentId: Guid.NewGuid(),
                StartDate: new DateOnly(2026, 6, 1),
                FrequencyDaysPerWeek: 4,
                VisibilityMode: PlanVisibilityMode.Guided,
                HideExercises: false,
                HideSetsReps: false,
                HideFutureWorkouts: false,
                DisableTraineeEditing: false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        // Rejected before any persistence work.
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_request_updates_configuration_in_place_and_saves()
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

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var newStartDate = new DateOnly(2026, 7, 1);
        var result = await sut.Handle(
            new UpdatePlanAssignmentCommand(
                AssignmentId: assignment.Id,
                StartDate: newStartDate,
                FrequencyDaysPerWeek: 5,
                VisibilityMode: PlanVisibilityMode.Blind,
                HideExercises: true,
                HideSetsReps: true,
                HideFutureWorkouts: true,
                DisableTraineeEditing: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value;
        Assert.True(value);

        // Domain configuration mutated in place from the command.
        Assert.Equal(newStartDate, assignment.StartDate);
        Assert.Equal(5, assignment.FrequencyDaysPerWeek);
        Assert.Equal(PlanVisibilityMode.Blind, assignment.VisibilityMode);
        Assert.True(assignment.HideExercises);
        Assert.True(assignment.HideSetsReps);
        Assert.True(assignment.HideFutureWorkouts);
        Assert.True(assignment.DisableTraineeEditing);

        // Committed exactly once.
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Null_start_date_leaves_existing_start_date_unchanged()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = CreateAssignment(tenantId, createdBy, traineeId, planId);
        var originalStartDate = assignment.StartDate;
        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>())
            .Returns(assignment);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new UpdatePlanAssignmentCommand(
                AssignmentId: assignment.Id,
                StartDate: null,
                FrequencyDaysPerWeek: 2,
                VisibilityMode: PlanVisibilityMode.Guided,
                HideExercises: false,
                HideSetsReps: false,
                HideFutureWorkouts: false,
                DisableTraineeEditing: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        // A null StartDate preserves the existing one while the rest is still applied.
        Assert.Equal(originalStartDate, assignment.StartDate);
        Assert.Equal(2, assignment.FrequencyDaysPerWeek);
        Assert.Equal(PlanVisibilityMode.Guided, assignment.VisibilityMode);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
