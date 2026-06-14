using BuildingBlocks.Shared.Nutrition;
using Xunit;

namespace Gymbro.Tests.Domain;

/// <summary>
/// The nutrition recurrence rule — the server source of truth (mirrored by both clients). Exhaustively pinned:
/// EveryDay always applies; TrainingDay/RestDay gate on the day's type; an unknown value fails open (a planned
/// meal is never silently dropped).
/// </summary>
public sealed class NutritionScheduleRulesTests
{
    [Theory]
    [InlineData(DayApplicability.EveryDay, true, true)]
    [InlineData(DayApplicability.EveryDay, false, true)]
    [InlineData(DayApplicability.TrainingDay, true, true)]
    [InlineData(DayApplicability.TrainingDay, false, false)]
    [InlineData(DayApplicability.RestDay, true, false)]
    [InlineData(DayApplicability.RestDay, false, true)]
    public void Applies_gates_meals_by_day_type(DayApplicability applicability, bool isTrainingDay, bool expected)
        => Assert.Equal(expected, NutritionScheduleRules.Applies(applicability, isTrainingDay));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unknown_applicability_fails_open(bool isTrainingDay)
        => Assert.True(NutritionScheduleRules.Applies((DayApplicability)999, isTrainingDay));
}
