using BuildingBlocks.Shared.DomainPrimitives;
using Modules.FoodModule.Entities;
using Xunit;

namespace Gymbro.Tests.Domain;

public sealed class FoodTests
{
    private static Food Global() => Food.CreateGlobal(
        "Chicken Breast", FoodKind.Food, "100 g", 100m, 165m, 31m, 0m, 3.6m, 0m, brand: null);

    [Fact]
    public void CreateGlobal_has_no_tenant_and_keeps_macros()
    {
        var food = Global();

        Assert.Null(food.TenantId); // global catalog (ISharedEntity)
        Assert.Equal("Chicken Breast", food.Name);
        Assert.Equal(FoodKind.Food, food.Kind);
        Assert.Equal(165m, food.EnergyKcal);
        Assert.Equal(31m, food.ProteinG);
        Assert.False(food.IsDeleted);
    }

    [Fact]
    public void CreateForTenant_is_owned_by_the_gym()
    {
        var tenantId = Guid.NewGuid();
        var food = Food.CreateForTenant(
            tenantId, "House Blend", FoodKind.Supplement, "1 scoop", 30m, 120m, 24m, 3m, 1.5m, 0m, "GymBro");

        Assert.Equal(tenantId, food.TenantId);
        Assert.Equal(FoodKind.Supplement, food.Kind);
    }

    [Fact]
    public void CreateForTenant_throws_when_tenant_is_empty()
    {
        Assert.Throws<DomainException>(() => Food.CreateForTenant(
            Guid.Empty, "X", FoodKind.Food, "100 g", null, null, null, null, null, null, null));
    }

    [Fact]
    public void Create_throws_when_name_missing()
    {
        Assert.Throws<DomainException>(() => Food.CreateGlobal(
            "  ", FoodKind.Food, "100 g", null, null, null, null, null, null, null));
    }

    [Fact]
    public void Create_throws_when_serving_label_missing()
    {
        Assert.Throws<DomainException>(() => Food.CreateGlobal(
            "Rice", FoodKind.Food, "", null, null, null, null, null, null, null));
    }

    [Fact]
    public void Create_throws_on_negative_macro()
    {
        Assert.Throws<DomainException>(() => Food.CreateGlobal(
            "Rice", FoodKind.Food, "100 g", 100m, -1m, null, null, null, null, null));
    }

    [Fact]
    public void Create_throws_on_non_positive_serving_grams()
    {
        Assert.Throws<DomainException>(() => Food.CreateGlobal(
            "Rice", FoodKind.Food, "100 g", 0m, null, null, null, null, null, null));
    }

    [Fact]
    public void Create_allows_a_supplement_with_no_nutrition_data()
    {
        var food = Food.CreateGlobal("Creatine", FoodKind.Supplement, "1 scoop", 5m, null, null, null, null, null, null);

        Assert.Null(food.EnergyKcal);
        Assert.Null(food.ProteinG);
    }

    [Fact]
    public void UpdateDetails_replaces_fields()
    {
        var food = Global();
        food.UpdateDetails("Chicken Thigh", FoodKind.Food, "100 g", 100m, 209m, 26m, 0m, 11m, 0m, "Brand");

        Assert.Equal("Chicken Thigh", food.Name);
        Assert.Equal(209m, food.EnergyKcal);
        Assert.Equal("Brand", food.Brand);
    }
}
