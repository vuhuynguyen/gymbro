using BuildingBlocks.Application.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the revoke (soft-delete) behavior for a nutrition-plan assignment: a missing assignment is rejected
/// with the literal <c>NotFound</c> code and never touches persistence, while a found assignment is removed
/// via the repository and committed with a single SaveChanges. Mirrors DeletePlanAssignmentHandlerTests.
/// Fully mocked — no database.
/// </summary>
public sealed class DeleteNutritionAssignmentHandlerTests
{
    private static DeleteNutritionAssignmentHandler CreateSut(
        INutritionPlanAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
        => new(assignmentRepository, unitOfWork);

    private static NutritionPlanAssignment CreateAssignment()
        => NutritionPlanAssignment.Create(
            tenantId: Guid.NewGuid(),
            createdBy: Guid.NewGuid(),
            traineeId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            planVersion: 1,
            startDate: new DateOnly(2026, 6, 1),
            endDate: null,
            visibilityMode: NutritionVisibilityMode.Full,
            hideMacroTargets: false,
            disableTraineeEditing: false,
            snapshotJson: null);

    [Fact]
    public async Task Missing_assignment_returns_not_found_and_never_persists()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        assignmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NutritionPlanAssignment?)null);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new DeleteNutritionAssignmentCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        assignmentRepository.DidNotReceive().Remove(Arg.Any<NutritionPlanAssignment>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_assignment_is_removed_and_committed_once()
    {
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var assignment = CreateAssignment();
        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(assignmentRepository, unitOfWork);

        var result = await sut.Handle(
            new DeleteNutritionAssignmentCommand(assignment.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        assignmentRepository.Received(1).Remove(assignment);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
