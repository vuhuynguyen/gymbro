using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Entities;

namespace Modules.FoodModule.Infrastructure.Persistence;

public sealed class FoodRepository(DbContext context) : Repository<Food>(context), IFoodRepository
{
}
