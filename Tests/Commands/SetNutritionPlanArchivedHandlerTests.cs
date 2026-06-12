using BuildingBlocks.Application.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the archive/unarchive lifecycle for a nutrition plan template: setting
/// <see cref="SetNutritionPlanArchivedCommand.Archived"/> flips <see cref="NutritionPlan.IsArchived"/> on the
/// loaded plan and persists it, and a missing plan returns NotFound before any save. The row-level tenant
/// check is the EF tenant filter on the repository (nutrition has no per-row author policy), so the handler
/// itself does no author gating — mirrors the other nutrition plan handlers. Fully mocked — no database.
/// </summary>
public sealed class SetNutritionPlanArchivedHandlerTests
{
    private static SetNutritionPlanArchivedHandler CreateSut(
        INutritionPlanRepository repository, IUnitOfWork unitOfWork)
        => new(repository, unitOfWork);

    private static NutritionPlan CreatePlan()
        => NutritionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "Cut Plan", null);

    [Fact]
    public async Task Archiving_plan_sets_flag_true_and_persists()
    {
        var plan = CreatePlan();
        var repository = Substitute.For<INutritionPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var sut = CreateSut(repository, unitOfWork);

        var result = await sut.Handle(
            new SetNutritionPlanArchivedCommand(plan.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(plan.IsArchived);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unarchiving_plan_sets_flag_false_and_persists()
    {
        var plan = CreatePlan();
        plan.SetArchived(true);

        var repository = Substitute.For<INutritionPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var sut = CreateSut(repository, unitOfWork);

        var result = await sut.Handle(
            new SetNutritionPlanArchivedCommand(plan.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(plan.IsArchived);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_plan_returns_not_found_without_saving()
    {
        var repository = Substitute.For<INutritionPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NutritionPlan?)null);

        var sut = CreateSut(repository, unitOfWork);

        var result = await sut.Handle(
            new SetNutritionPlanArchivedCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
