using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands.Handlers;
using Modules.WorkoutPlanModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the archive/unarchive lifecycle for a workout plan template: setting
/// <see cref="SetWorkoutPlanArchivedCommand.Archived"/> flips <see cref="WorkoutPlan.IsArchived"/> on the
/// loaded plan and persists it, a missing plan returns NotFound before any save, and only the plan's
/// author (or an admin) may mutate it — a non-author is Forbidden and nothing is persisted.
/// Fully mocked — no database.
/// </summary>
public sealed class SetWorkoutPlanArchivedHandlerTests
{
    private static SetWorkoutPlanArchivedHandler CreateSut(
        IWorkoutPlanRepository repository,
        IUnitOfWork unitOfWork,
        Guid userId,
        bool isAdmin = false)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.IsAdmin.Returns(isAdmin);

        return new SetWorkoutPlanArchivedHandler(repository, unitOfWork, currentUser);
    }

    private static WorkoutPlan CreatePlan(Guid authorId)
    {
        var tenantId = Guid.NewGuid();
        return WorkoutPlan.Create(tenantId, authorId, "Strength Block", null, 8, 4);
    }

    [Fact]
    public async Task Author_archiving_plan_sets_flag_true_and_persists()
    {
        var authorId = Guid.NewGuid();
        var plan = CreatePlan(authorId);

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var sut = CreateSut(repository, unitOfWork, authorId);

        var result = await sut.Handle(
            new SetWorkoutPlanArchivedCommand(plan.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(plan.IsArchived);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Author_unarchiving_plan_sets_flag_false_and_persists()
    {
        var authorId = Guid.NewGuid();
        var plan = CreatePlan(authorId);
        plan.SetArchived(true);

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var sut = CreateSut(repository, unitOfWork, authorId);

        var result = await sut.Handle(
            new SetWorkoutPlanArchivedCommand(plan.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(plan.IsArchived);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_plan_returns_not_found_without_saving()
    {
        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutPlan?)null);

        var sut = CreateSut(repository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(
            new SetWorkoutPlanArchivedCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_author_non_admin_is_forbidden_and_plan_is_untouched()
    {
        var authorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var plan = CreatePlan(authorId);

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var sut = CreateSut(repository, unitOfWork, otherUserId);

        var result = await sut.Handle(
            new SetWorkoutPlanArchivedCommand(plan.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);
        Assert.False(plan.IsArchived);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
