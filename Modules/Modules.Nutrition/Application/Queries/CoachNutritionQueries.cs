using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.DTOs;

namespace Modules.NutritionModule.Application.Queries;

// Coach surface: a gym's client nutrition days, tenant-scoped (the EF filter bounds these to the coach's
// own gym), gated on NutritionLogViewAll — mirrors the coach view of workout sessions.

public sealed record ListTraineeNutritionDaysQuery(
    Guid TraineeId,
    DateOnly? From,
    DateOnly? To,
    int Page = 1,
    int PageSize = 30) : IRequest<Result<DailyNutritionLogListDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionLogViewAll;
}

public sealed record GetTraineeNutritionDayQuery(Guid TraineeId, DateOnly Date)
    : IRequest<Result<DailyNutritionLogDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionLogViewAll;
}
