using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;
using Modules.NutritionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Queries.Handlers;

/// <summary>A specific past/current day for the caller (self-scoped). 404 when none exists for that date.</summary>
public sealed class GetMyNutritionDayHandler(
    IDailyNutritionLogRepository logRepository,
    INutritionPlanAssignmentRepository assignmentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyNutritionDayQuery, Result<DailyNutritionLogDto>>
{
    public async Task<Result<DailyNutritionLogDto>> Handle(GetMyNutritionDayQuery request, CancellationToken cancellationToken)
    {
        var log = await logRepository.GetOwnByDateAsync(currentUser.UserId, request.Date, cancellationToken);
        if (log == null)
            return Result<DailyNutritionLogDto>.Failure(NotFound("NotFound", "No nutrition log for that date."));

        var dto = NutritionMapping.ToDayDto(log);
        dto = await NutritionDayVisibility.ApplyAsync(
            log, dto, assignmentRepository, currentUser.UserId, cancellationToken);
        return Result<DailyNutritionLogDto>.Success(dto);
    }
}
