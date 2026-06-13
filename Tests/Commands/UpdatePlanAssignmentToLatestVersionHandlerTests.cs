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
/// Apply-latest re-points an assignment onto the newest plan version, but it must never blank an existing
/// snapshot: when the caller supplies no snapshot the assignment keeps its current one (only a non-blank
/// caller value overwrites it). It also refuses to advance onto an archived latest version (Conflict) and
/// fails NotFound when the assignment or plan is missing. Fully mocked — no database.
/// </summary>
public sealed class UpdatePlanAssignmentToLatestVersionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TraineeId = Guid.NewGuid();

    private static UpdatePlanAssignmentToLatestVersionHandler CreateSut(
        IPlanAssignmentRepository assignmentRepository,
        IWorkoutPlanRepository workoutPlanRepository,
        IUnitOfWork unitOfWork)
        => new(assignmentRepository, workoutPlanRepository, unitOfWork);

    private static WorkoutPlan CreatePlanV1()
        => WorkoutPlan.Create(TenantId, ActorId, "Plan", null, null, null);

    // The next PUBLISHED version off v1 (apply-latest only ever advances onto published versions).
    private static WorkoutPlan CreatePublishedNext(WorkoutPlan v1)
    {
        var next = WorkoutPlan.CreateDraft(v1, ActorId, v1.Version + 1, "Plan", null, null, null);
        next.Publish();
        return next;
    }

    private static PlanAssignment CreateAssignmentOnV1(WorkoutPlan plan, string? snapshotJson)
        => PlanAssignment.Create(
            TenantId,
            ActorId,
            TraineeId,
            plan.Id,
            plan.Version,
            new DateOnly(2026, 1, 1),
            3,
            PlanVisibilityMode.Full,
            false,
            false,
            false,
            false,
            snapshotJson);

    [Fact]
    public async Task Missing_assignment_returns_not_found_without_loading_plan()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var workoutPlanRepository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        assignmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PlanAssignment?)null);

        var sut = CreateSut(assignmentRepository, workoutPlanRepository, unitOfWork);

        var result = await sut.Handle(
            new UpdatePlanAssignmentToLatestVersionCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await workoutPlanRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archived_latest_version_returns_conflict_and_does_not_advance()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var workoutPlanRepository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var currentPlan = CreatePlanV1();
        var assignment = CreateAssignmentOnV1(currentPlan, "{\"v\":1}");

        // Newest version in the template is archived → apply-latest must refuse to advance onto it.
        var latest = CreatePublishedNext(currentPlan);
        latest.SetArchived(true);

        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        workoutPlanRepository.GetByIdAsync(currentPlan.Id, Arg.Any<CancellationToken>()).Returns(currentPlan);
        workoutPlanRepository.GetLatestPublishedVersionInTemplateAsync(currentPlan.TemplateId, Arg.Any<CancellationToken>())
            .Returns(latest);

        var sut = CreateSut(assignmentRepository, workoutPlanRepository, unitOfWork);

        var result = await sut.Handle(
            new UpdatePlanAssignmentToLatestVersionCommand(assignment.Id, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        // Assignment stayed on its original version and never persisted.
        Assert.Equal(currentPlan.Id, assignment.PlanId);
        Assert.Equal(currentPlan.Version, assignment.PlanVersion);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_latest_with_no_snapshot_repoints_version_and_preserves_existing_snapshot()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var workoutPlanRepository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        const string existingSnapshot = "{\"frozen\":\"v1\"}";
        var currentPlan = CreatePlanV1();
        var assignment = CreateAssignmentOnV1(currentPlan, existingSnapshot);

        var latest = CreatePublishedNext(currentPlan);

        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        workoutPlanRepository.GetByIdAsync(currentPlan.Id, Arg.Any<CancellationToken>()).Returns(currentPlan);
        workoutPlanRepository.GetLatestPublishedVersionInTemplateAsync(currentPlan.TemplateId, Arg.Any<CancellationToken>())
            .Returns(latest);

        var sut = CreateSut(assignmentRepository, workoutPlanRepository, unitOfWork);

        // Caller supplies no snapshot → the handler must keep the current one, never null it.
        var result = await sut.Handle(
            new UpdatePlanAssignmentToLatestVersionCommand(assignment.Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);

        // Re-pointed to the newest version...
        Assert.Equal(latest.Id, assignment.PlanId);
        Assert.Equal(latest.Version, assignment.PlanVersion);
        // ...while the original snapshot is preserved verbatim (blank caller value does not blank it).
        Assert.Equal(existingSnapshot, assignment.SnapshotJson);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_latest_with_fresh_snapshot_overwrites_existing_snapshot()
    {
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var workoutPlanRepository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var currentPlan = CreatePlanV1();
        var assignment = CreateAssignmentOnV1(currentPlan, "{\"frozen\":\"v1\"}");

        var latest = CreatePublishedNext(currentPlan);

        assignmentRepository.GetByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        workoutPlanRepository.GetByIdAsync(currentPlan.Id, Arg.Any<CancellationToken>()).Returns(currentPlan);
        workoutPlanRepository.GetLatestPublishedVersionInTemplateAsync(currentPlan.TemplateId, Arg.Any<CancellationToken>())
            .Returns(latest);

        var sut = CreateSut(assignmentRepository, workoutPlanRepository, unitOfWork);

        const string fresh = "{\"frozen\":\"v2\"}";
        var result = await sut.Handle(
            new UpdatePlanAssignmentToLatestVersionCommand(assignment.Id, fresh), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(latest.Version, assignment.PlanVersion);
        // A non-blank caller value replaces the snapshot.
        Assert.Equal(fresh, assignment.SnapshotJson);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
