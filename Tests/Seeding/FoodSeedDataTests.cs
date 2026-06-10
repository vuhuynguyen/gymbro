using Modules.FoodModule.Entities;
using Modules.FoodModule.Infrastructure.Seeding;
using Xunit;

namespace Gymbro.Tests.Seeding;

/// <summary>
/// Guards the food master-data seed pipeline (loader → validator → factory). Pure (no database), so it runs
/// everywhere and fails the build if someone adds a food with a bad kind, duplicate name, empty serving label,
/// or negative macro before it can reach the database. Mirrors <c>ExerciseSeedDataTests</c>.
/// </summary>
public sealed class FoodSeedDataTests
{
    private static FoodSeedData Load() => new FoodSeedDataLoader().Load();

    [Fact]
    public void Seed_file_loads_with_a_useful_starter_catalog()
    {
        var data = Load();

        Assert.True(data.Foods.Count >= 30,
            $"Expected a useful starter catalog (>= 30 foods); found {data.Foods.Count}.");
        Assert.Contains(data.Foods, f => string.Equals(f.Name, "Chicken Breast, Cooked", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Embedded_seed_data_passes_validation()
    {
        var result = new FoodSeedDataValidator().Validate(Load());

        Assert.True(result.IsValid,
            "Embedded seed data failed validation:\n" + string.Join("\n", result.Errors));
    }

    [Fact]
    public void Catalog_covers_every_food_kind()
    {
        var kinds = Load().Foods
            .Select(f => Enum.Parse<FoodKind>(f.Kind!, ignoreCase: true))
            .ToHashSet();

        Assert.Contains(FoodKind.Food, kinds);
        Assert.Contains(FoodKind.Supplement, kinds);
        Assert.Contains(FoodKind.Beverage, kinds);
    }

    [Fact]
    public void Factory_builds_a_valid_global_food_for_every_entry()
    {
        foreach (var dto in Load().Foods)
        {
            var food = FoodSeedFactory.Create(dto); // throws DomainException on any invariant violation
            Assert.Null(food.TenantId); // global catalog
            Assert.False(string.IsNullOrWhiteSpace(food.Name));
            Assert.False(string.IsNullOrWhiteSpace(food.ServingLabel));
        }
    }
}
