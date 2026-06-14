using BuildingBlocks.Shared.Nutrition;
using BuildingBlocks.Shared.DomainPrimitives;
using Modules.NutritionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Domain;

public sealed class NutritionPlanTests
{
    private static PlanMealData Meal(string name, int order, params PlanMealItemData[] items) =>
        new(name, order, new TimeOnly(8, 0), DayApplicability.EveryDay, items);

    private static PlanMealItemData Item(int order) =>
        new(Guid.NewGuid(), order, 1m, "Oats", "1 bowl", 300m, 10m, 50m, 6m, 8m);

    [Fact]
    public void Create_starts_at_version_1_with_a_fresh_template()
    {
        var plan = NutritionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "Cut Plan", "desc");

        Assert.Equal(1, plan.Version);
        Assert.NotEqual(Guid.Empty, plan.TemplateId);
        Assert.Equal("Cut Plan", plan.Name);
        Assert.True(plan.IsDraft);
        Assert.False(plan.IsDeleted);
    }

    [Fact]
    public void Publish_clears_the_draft_flag_and_then_throws_if_published_again()
    {
        var plan = NutritionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "Plan", null);

        plan.Publish();
        Assert.False(plan.IsDraft);

        Assert.Throws<DomainException>(() => plan.Publish());
    }

    [Fact]
    public void Create_throws_when_name_missing()
    {
        Assert.Throws<DomainException>(() =>
            NutritionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "  ", null));
    }

    [Fact]
    public void ReplaceStructure_builds_meals_and_items_in_order()
    {
        var plan = NutritionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "Plan", null);

        plan.ReplaceStructure(new[]
        {
            Meal("Lunch", 2, Item(1)),
            Meal("Breakfast", 1, Item(1), Item(2)),
        });

        Assert.Equal(2, plan.Meals.Count);
        Assert.Equal("Breakfast", plan.Meals.OrderBy(m => m.Order).First().Name);
        Assert.Equal(2, plan.Meals.OrderBy(m => m.Order).First().Items.Count);
    }

    [Fact]
    public void ReplaceStructure_is_idempotent_replacement_not_append()
    {
        var plan = NutritionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "Plan", null);
        plan.ReplaceStructure(new[] { Meal("Breakfast", 1, Item(1)) });
        plan.ReplaceStructure(new[] { Meal("Dinner", 1, Item(1)) });

        Assert.Single(plan.Meals);
        Assert.Equal("Dinner", plan.Meals.First().Name);
    }

    [Fact]
    public void CreateDraft_deep_copies_structure_at_the_caller_supplied_version()
    {
        var plan = NutritionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "Plan", null);
        plan.ReplaceStructure(new[] { Meal("Breakfast", 1, Item(1), Item(2)) });

        var v2 = NutritionPlan.CreateDraft(plan, Guid.NewGuid(), plan.Version + 1, "Plan v2", "new");

        Assert.Equal(plan.TemplateId, v2.TemplateId); // same template chain
        Assert.Equal(2, v2.Version);
        Assert.True(v2.IsDraft);
        Assert.NotEqual(plan.Id, v2.Id);
        Assert.Single(v2.Meals);
        Assert.Equal(2, v2.Meals.First().Items.Count); // structure carried over
        // Deep copy: the new version's meals are distinct instances.
        Assert.NotEqual(plan.Meals.First().Id, v2.Meals.First().Id);
    }
}
