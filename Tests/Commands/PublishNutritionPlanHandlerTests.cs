using BuildingBlocks.Application.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the nutrition publish guards (mirrors PublishWorkoutPlanHandlerTests, minus the author policy which the
/// nutrition module does not apply): NotFound for a missing plan, Conflict when the head is already published,
/// and a happy path that flips the draft to published and commits once. Fully mocked — no database.
/// </summary>
public sealed class PublishNutritionPlanHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Coach = Guid.NewGuid();

    private static PublishNutritionPlanHandler CreateSut(INutritionPlanRepository repository, IUnitOfWork unitOfWork)
        => new(repository, unitOfWork);

    private static NutritionPlan DraftPlan() => NutritionPlan.Create(TenantId, Coach, "Plan", null);

    [Fact]
    public async Task Missing_plan_returns_not_found()
    {
        var repository = Substitute.For<INutritionPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((NutritionPlan?)null);

        var result = await CreateSut(repository, unitOfWork)
            .Handle(new PublishNutritionPlanCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Already_published_head_is_conflict()
    {
        var repository = Substitute.For<INutritionPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = DraftPlan();
        plan.Publish();
        repository.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await CreateSut(repository, unitOfWork)
            .Handle(new PublishNutritionPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publishing_a_draft_head_flips_it_published_and_returns_the_version()
    {
        var repository = Substitute.For<INutritionPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = DraftPlan();
        repository.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await CreateSut(repository, unitOfWork)
            .Handle(new PublishNutritionPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(plan.Version, result.Value);
        Assert.False(plan.IsDraft);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
