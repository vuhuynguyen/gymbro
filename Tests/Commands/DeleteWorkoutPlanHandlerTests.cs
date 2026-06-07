using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands.Handlers;
using Modules.WorkoutPlanModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the plan-deletion lifecycle rule: a plan version may not be deleted while a live
/// <see cref="PlanAssignment"/> still pins it (returns Conflict, leaving structure and header intact);
/// when nothing pins it the header is soft-deleted (child structure cleared, then SaveChanges); and a
/// missing plan returns NotFound. Fully mocked — the only "database" is an EF InMemory context used solely
/// to give <see cref="IPlanAssignmentRepository.Query"/> a real async-capable provider for AnyAsync.
/// </summary>
public sealed class DeleteWorkoutPlanHandlerTests
{
    private static DeleteWorkoutPlanHandler CreateSut(
        IWorkoutPlanRepository repository,
        IPlanAssignmentRepository assignmentRepository,
        IUnitOfWork unitOfWork,
        Guid userId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.IsAdmin.Returns(false);

        return new DeleteWorkoutPlanHandler(repository, assignmentRepository, unitOfWork, currentUser);
    }

    private static WorkoutPlan AuthoredPlan(Guid tenantId, Guid authorId) =>
        WorkoutPlan.Create(tenantId, authorId, "Push/Pull/Legs", null, durationWeeks: 4, workoutsPerWeek: 3);

    /// <summary>Admin-stubbed context services so EF global filters never exclude the seeded assignment.</summary>
    private static AppDbContext NewDb() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"delete-plan-{Guid.NewGuid()}")
                .Options,
            new StubDbContextServices());

    [Fact]
    public async Task Missing_plan_returns_NotFound_and_touches_nothing()
    {
        var repository = Substitute.For<IWorkoutPlanRepository>();
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var planId = Guid.NewGuid();
        repository.GetForUpdateAsync(planId, Arg.Any<CancellationToken>())
            .Returns((WorkoutPlan?)null);

        var sut = CreateSut(repository, assignmentRepository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(new DeleteWorkoutPlanCommand(planId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await repository.DidNotReceive().ClearPlanStructureAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_author_non_admin_is_forbidden_and_plan_is_untouched()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var plan = AuthoredPlan(tenantId, authorId);

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        // Caller is a different, non-admin user → the authorship guard rejects before any pin check.
        var sut = CreateSut(repository, assignmentRepository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(new DeleteWorkoutPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);

        Assert.False(plan.IsDeleted);
        await repository.DidNotReceive().ClearPlanStructureAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Live_assignment_pinning_the_plan_blocks_delete_with_Conflict()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var plan = AuthoredPlan(tenantId, authorId);

        await using var db = NewDb();
        db.PlanAssignments.Add(PlanAssignment.Create(
            tenantId,
            createdBy: authorId,
            traineeId: Guid.NewGuid(),
            planId: plan.Id,
            planVersion: plan.Version,
            startDate: new DateOnly(2026, 1, 1),
            frequencyDaysPerWeek: 3,
            visibilityMode: PlanVisibilityMode.Full,
            hideExercises: false,
            hideSetsReps: false,
            hideFutureWorkouts: false,
            disableTraineeEditing: false,
            snapshotJson: null));
        await db.SaveChangesAsync();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        assignmentRepository.Query().Returns(db.PlanAssignments);

        var sut = CreateSut(repository, assignmentRepository, unitOfWork, authorId);

        var result = await sut.Handle(new DeleteWorkoutPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        // Plan left intact: neither the child structure nor the soft-delete is applied.
        Assert.False(plan.IsDeleted);
        await repository.DidNotReceive().ClearPlanStructureAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_live_assignment_soft_deletes_after_clearing_structure()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var plan = AuthoredPlan(tenantId, authorId);

        // Empty assignment set → AnyAsync(...) is false.
        await using var db = NewDb();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        assignmentRepository.Query().Returns(db.PlanAssignments);

        var sut = CreateSut(repository, assignmentRepository, unitOfWork, authorId);

        var result = await sut.Handle(new DeleteWorkoutPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Child structure hard-deleted before the header soft-delete, which is persisted exactly once.
        Assert.True(plan.IsDeleted);
        await repository.Received(1).ClearPlanStructureAsync(plan.Id, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Minimal context services for an HTTP-less test: admin so EF global filters never exclude rows.</summary>
    private sealed class StubDbContextServices : IDbContextServices, ICurrentUser, ITenantContext
    {
        public ICurrentUser CurrentUser => this;
        public ITenantContext TenantContext => this;
        public IPublisher Publisher => throw new NotSupportedException("AppDbContext no longer publishes inline.");
        public Guid UserId => Guid.Empty;
        public bool IsAdmin => true;
        public Guid? TenantId => null;
    }
}
