using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Queries;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the self-scoped daily-log write guards: a closed day rejects edits (Conflict <c>DailyLog.Closed</c>),
/// completion-first status changes persist, and ad-hoc logging requires a known food. Fully mocked.
/// </summary>
public sealed class NutritionLogHandlerTests
{
    private static readonly DateOnly Date = new(2026, 6, 10);

    private static DailyNutritionLog SeededOpenLog(out Guid itemId)
    {
        var log = DailyNutritionLog.Open(
            Guid.NewGuid(), Guid.NewGuid(), Date, "UTC", NutritionSource.FromAssignment, Guid.NewGuid(), "{}");
        log.SeedPlannedItems(new[]
        {
            new LoggedItemData(Guid.NewGuid(), "Breakfast", new TimeOnly(8, 0), 1, Guid.NewGuid(),
                "Food", "Oats", "1 bowl", 1m, 300m, 10m, 50m, 6m, 8m)
        });
        itemId = log.Items.First().Id;
        return log;
    }

    private static (SetNutritionItemStatusHandler Sut, IUnitOfWork Uow) StatusSut(DailyNutritionLog? log)
    {
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.GetOwnByDateAsync(Arg.Any<Guid>(), Date, Arg.Any<CancellationToken>()).Returns(log);
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());
        return (new SetNutritionItemStatusHandler(repo, uow, currentUser), uow);
    }

    [Fact]
    public async Task Complete_marks_item_completed_and_saves()
    {
        var log = SeededOpenLog(out var itemId);
        var (sut, uow) = StatusSut(log);

        var result = await sut.Handle(
            new SetNutritionItemStatusCommand(Date, itemId, LoggedItemStatus.Completed, "done"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(LoggedItemStatus.Completed, log.FindItem(itemId)!.Status);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Editing_a_closed_day_is_rejected()
    {
        var log = SeededOpenLog(out var itemId);
        log.Close();
        var (sut, _) = StatusSut(log);

        var result = await sut.Handle(
            new SetNutritionItemStatusCommand(Date, itemId, LoggedItemStatus.Completed, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DailyLog.Closed", result.Error.Code);
    }

    [Fact]
    public async Task Missing_day_returns_not_found()
    {
        var (sut, _) = StatusSut(log: null);

        var result = await sut.Handle(
            new SetNutritionItemStatusCommand(Date, Guid.NewGuid(), LoggedItemStatus.Completed, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Adhoc_with_unknown_food_is_rejected()
    {
        var log = SeededOpenLog(out _);
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.GetOwnByDateAsync(Arg.Any<Guid>(), Date, Arg.Any<CancellationToken>()).Returns(log);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveFoodSummariesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, FoodSummaryDto>>.Success(new Dictionary<Guid, FoodSummaryDto>()));
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());

        var sut = new AddAdhocNutritionItemHandler(repo, mediator, Substitute.For<IUnitOfWork>(), currentUser);

        var result = await sut.Handle(
            new AddAdhocNutritionItemCommand(Date, Guid.NewGuid(), 1m, "Snack", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FoodId", result.Error.Code);
    }

    [Fact]
    public async Task Adhoc_with_known_food_appends_a_completed_item()
    {
        var log = SeededOpenLog(out _);
        var foodId = Guid.NewGuid();
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.GetOwnByDateAsync(Arg.Any<Guid>(), Date, Arg.Any<CancellationToken>()).Returns(log);
        var mediator = Substitute.For<IMediator>();
        var dict = new Dictionary<Guid, FoodSummaryDto>
        {
            [foodId] = new(foodId, "Banana", "Food", "1 medium", 105m, 1.3m, 27m, 0.4m, 3m)
        };
        mediator.Send(Arg.Any<ResolveFoodSummariesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, FoodSummaryDto>>.Success(dict));
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());

        var sut = new AddAdhocNutritionItemHandler(repo, mediator, uow, currentUser);

        var result = await sut.Handle(
            new AddAdhocNutritionItemCommand(Date, foodId, 1m, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = log.Items.Single(i => i.FoodId == foodId);
        Assert.Null(added.PlanMealItemId);
        Assert.Equal(LoggedItemStatus.Completed, added.Status);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Adhoc_with_inline_custom_food_logs_a_kinded_item_without_a_food_id()
    {
        var log = SeededOpenLog(out _);
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.GetOwnByDateAsync(Arg.Any<Guid>(), Date, Arg.Any<CancellationToken>()).Returns(log);
        var mediator = Substitute.For<IMediator>(); // never queried for a custom food
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());

        var sut = new AddAdhocNutritionItemHandler(repo, mediator, uow, currentUser);

        var result = await sut.Handle(
            new AddAdhocNutritionItemCommand(Date, null, 1m, "Supplements", null,
                CustomName: "Creatine", CustomKind: "supplement", ServingLabel: "5 g", EnergyKcal: 0m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = log.Items.Single(i => i.FoodNameSnapshot == "Creatine");
        Assert.Null(added.FoodId);
        Assert.Equal("supplement", added.Kind);
        Assert.Equal(LoggedItemStatus.Completed, added.Status);
        await mediator.DidNotReceive().Send(Arg.Any<ResolveFoodSummariesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_deletes_an_adhoc_item_but_refuses_a_planned_one()
    {
        var log = SeededOpenLog(out var plannedId);
        // Seed one ad-hoc item directly via the domain.
        log.AddAdhocItem(new LoggedItemData(null, "Off-plan", null, 0, Guid.NewGuid(), "Food",
            "Donut", "1", 1m, 300m, null, null, null, null), null);
        var adhocId = log.Items.Single(i => i.PlanMealItemId == null).Id;

        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.GetOwnByDateAsync(Arg.Any<Guid>(), Date, Arg.Any<CancellationToken>()).Returns(log);
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());
        var sut = new RemoveNutritionItemHandler(repo, uow, currentUser);

        var planned = await sut.Handle(new RemoveNutritionItemCommand(Date, plannedId), CancellationToken.None);
        Assert.True(planned.IsFailure); // planned items are skipped, not removed

        var ok = await sut.Handle(new RemoveNutritionItemCommand(Date, adhocId), CancellationToken.None);
        Assert.True(ok.IsSuccess);
        Assert.DoesNotContain(log.Items, i => i.Id == adhocId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
