using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.DTOs;

namespace Modules.FoodModule.Application.Queries;

/// <summary>
/// Searches the food catalog (global + the active tenant's custom foods, via the ISharedEntity filter).
/// Admin sees everything; a member needs <c>PlanView</c> — the same admin-vs-tenant split as exercise search,
/// so this is imperatively guarded in the handler.
/// </summary>
public sealed record SearchFoodsQuery(
    string? Search,
    string? Kind,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<FoodListDto>>;

public sealed record GetFoodByIdQuery(Guid Id) : IRequest<Result<FoodDto>>;
