using BuildingBlocks.Application.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the pause/resume rule for a nutrition-plan assignment: pausing flips
/// <see cref="NutritionPlanAssignment.IsActive"/> to false and resuming flips it back to true, with the change
/// persisted via SaveChanges. A missing assignment maps to a clean NotFound. Mirrors
/// SetPlanAssignmentActiveHandlerTests. Fully mocked — no database.
/// </summary>
public sealed class SetNutritionAssignmentActiveHandlerTests
{
    private static SetNutritionAssignmentActiveHandler CreateSut(
        INutritionPlanAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
        => new(assignmentRepository, unitOfWork);

    private static NutritionPlanAssignment BuildAssignment()
        => NutritionPlanAssignment.Create(
            tenantId: Guid.NewGuid(),
            createdBy: Guid.NewGuid(),
            traineeId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            planVersion: 1,
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            visibilityMode: NutritionVisibilityMode.Full,
            hideMacroTargets: false,
            disableTraineeEditing: false,
            snapshotJson: null);

    [Fact]
    public async Task Pausing_sets_inactive_and_saves()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = BuildAssignment();
        Assert.True(assignment.IsActive); // factory seeds active

        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new SetNutritionAssignmentActiveCommand(assignment.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(assignment.IsActive);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resuming_sets_active_and_saves()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = BuildAssignment();
        assignment.SetActive(false); // start paused

        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new SetNutritionAssignmentActiveCommand(assignment.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(assignment.IsActive);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_assignment_returns_not_found_without_saving()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        assignmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NutritionPlanAssignment?)null);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new SetNutritionAssignmentActiveCommand(Guid.NewGuid(), false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
