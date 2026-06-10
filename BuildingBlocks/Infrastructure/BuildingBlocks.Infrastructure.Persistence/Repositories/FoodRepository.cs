using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class FoodRepository(AppDbContext context) : Repository<Food>(context), IFoodRepository
{
}
