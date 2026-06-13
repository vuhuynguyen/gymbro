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
/// Pins the publish guards: a missing plan is NotFound; publishing when the head is already published is a
/// Conflict ("nothing to publish"); a non-author is Forbidden; and the happy path flips the draft to published
/// and commits once, returning the published version number. Fully mocked — no database.
/// </summary>
public sealed class PublishWorkoutPlanHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Author = Guid.NewGuid();

    private static PublishWorkoutPlanHandler CreateSut(
        IWorkoutPlanRepository repository, IUnitOfWork unitOfWork, Guid currentUserId, bool isAdmin = false)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);
        currentUser.IsAdmin.Returns(isAdmin);
        return new PublishWorkoutPlanHandler(repository, unitOfWork, currentUser);
    }

    private static WorkoutPlan DraftPlan() => WorkoutPlan.Create(TenantId, Author, "Plan", null, null, null);

    [Fact]
    public async Task Missing_plan_returns_not_found()
    {
        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((WorkoutPlan?)null);

        var result = await CreateSut(repository, unitOfWork, Author)
            .Handle(new PublishWorkoutPlanCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nothing_to_publish_when_head_is_already_published_is_conflict()
    {
        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = DraftPlan();
        plan.Publish(); // head is already published
        repository.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await CreateSut(repository, unitOfWork, Author)
            .Handle(new PublishWorkoutPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_author_is_forbidden()
    {
        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = DraftPlan();
        repository.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await CreateSut(repository, unitOfWork, Guid.NewGuid())
            .Handle(new PublishWorkoutPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publishing_a_draft_head_flips_it_published_and_returns_the_version()
    {
        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = DraftPlan();
        repository.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await CreateSut(repository, unitOfWork, Author)
            .Handle(new PublishWorkoutPlanCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(plan.Version, result.Value);
        Assert.False(plan.IsDraft);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
