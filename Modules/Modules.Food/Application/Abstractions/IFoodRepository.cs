using BuildingBlocks.Application.Abstractions;
using Modules.FoodModule.Entities;

namespace Modules.FoodModule.Application.Abstractions;

/// <summary>
/// Per-module repository for the food catalog (mirrors <c>IExerciseRepository</c>). Food has no child
/// collections, so it adds nothing over the generic contract — but every GymBro module exposes its own
/// repository abstraction rather than injecting <c>IRepository&lt;T&gt;</c> directly, so this keeps the
/// pattern consistent.
/// </summary>
public interface IFoodRepository : IRepository<Food>
{
}
