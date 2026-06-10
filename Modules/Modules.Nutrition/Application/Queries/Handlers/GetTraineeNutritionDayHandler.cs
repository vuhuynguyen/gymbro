using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Queries.Handlers;

public sealed class GetTraineeNutritionDayHandler(IDailyNutritionLogRepository logRepository)
    : IRequestHandler<GetTraineeNutritionDayQuery, Result<DailyNutritionLogDto>>
{
    public async Task<Result<DailyNutritionLogDto>> Handle(GetTraineeNutritionDayQuery request, CancellationToken cancellationToken)
    {
        // Tenant-filtered: a coach only sees days stamped with their own gym.
        var log = await logRepository.Query()
            .AsNoTracking()
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.TraineeId == request.TraineeId && l.LocalDate == request.Date, cancellationToken);

        return log == null
            ? Result<DailyNutritionLogDto>.Failure(NotFound("NotFound", "No nutrition log for that date."))
            : Result<DailyNutritionLogDto>.Success(NutritionMapping.ToDayDto(log));
    }
}
