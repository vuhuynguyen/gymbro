using BuildingBlocks.Application.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the update-assignment guards for nutrition: a missing assignment is rejected with the literal
/// <c>NotFound</c> code and never touches persistence, while a valid request mutates the assignment's
/// configuration in place (keeping the pinned version + snapshot) and commits via a single SaveChanges.
/// A null StartDate leaves the existing one unchanged. Mirrors UpdatePlanAssignmentHandlerTests, adapted
/// to nutrition's fields. Fully mocked — no database.
/// </summary>
public sealed class UpdateNutritionAssignmentHandlerTests
{
    private static UpdateNutritionAssignmentHandler CreateSut(
        INutritionPlanAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
        => new(assignmentRepository, unitOfWork);

    private static NutritionPlanAssignment CreateAssignment()
        => NutritionPlanAssignment.Create(
            tenantId: Guid.NewGuid(),
            createdBy: Guid.NewGuid(),
            traineeId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            planVersion: 1,
            startDate: new DateOnly(2026, 5, 1),
            endDate: null,
            visibilityMode: NutritionVisibilityMode.Full,
            hideMacroTargets: false,
            disableTraineeEditing: false,
            snapshotJson: "{\"snapshot\":true}");

    [Fact]
    public async Task Missing_assignment_returns_not_found_and_never_persists()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        assignmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NutritionPlanAssignment?)null);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new UpdateNutritionAssignmentCommand(
                AssignmentId: Guid.NewGuid(),
                StartDate: new DateOnly(2026, 6, 1),
                EndDate: null,
                VisibilityMode: NutritionVisibilityMode.Guided,
                HideMacroTargets: false,
                DisableTraineeEditing: false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_request_updates_configuration_in_place_and_saves()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = CreateAssignment();
        var pinnedPlanId = assignment.PlanId;
        var pinnedSnapshot = assignment.SnapshotJson;
        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var newStart = new DateOnly(2026, 7, 1);
        var newEnd = new DateOnly(2026, 8, 1);
        var result = await sut.Handle(
            new UpdateNutritionAssignmentCommand(
                AssignmentId: assignment.Id,
                StartDate: newStart,
                EndDate: newEnd,
                VisibilityMode: NutritionVisibilityMode.Blind,
                HideMacroTargets: true,
                DisableTraineeEditing: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);

        Assert.Equal(newStart, assignment.StartDate);
        Assert.Equal(newEnd, assignment.EndDate);
        Assert.Equal(NutritionVisibilityMode.Blind, assignment.VisibilityMode);
        Assert.True(assignment.HideMacroTargets);
        Assert.True(assignment.DisableTraineeEditing);

        // The pinned version + snapshot are untouched by an edit.
        Assert.Equal(pinnedPlanId, assignment.PlanId);
        Assert.Equal(pinnedSnapshot, assignment.SnapshotJson);

        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Null_start_date_leaves_existing_start_date_unchanged()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = CreateAssignment();
        var originalStart = assignment.StartDate;
        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new UpdateNutritionAssignmentCommand(
                AssignmentId: assignment.Id,
                StartDate: null,
                EndDate: null,
                VisibilityMode: NutritionVisibilityMode.Guided,
                HideMacroTargets: true,
                DisableTraineeEditing: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(originalStart, assignment.StartDate);
        Assert.Null(assignment.EndDate);
        Assert.Equal(NutritionVisibilityMode.Guided, assignment.VisibilityMode);
        Assert.True(assignment.HideMacroTargets);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
