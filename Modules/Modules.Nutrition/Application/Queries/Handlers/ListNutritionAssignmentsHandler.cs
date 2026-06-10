using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;

namespace Modules.NutritionModule.Application.Queries.Handlers;

public sealed class ListNutritionAssignmentsHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    INutritionPlanRepository planRepository)
    : IRequestHandler<ListNutritionAssignmentsQuery, Result<NutritionAssignmentListDto>>
{
    public async Task<Result<NutritionAssignmentListDto>> Handle(ListNutritionAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);

        // Tenant-filtered join to the plan for the display name (both queries are gym-scoped by the filter).
        var query = assignmentRepository.Query()
            .Join(planRepository.Query(),
                a => a.PlanId,
                p => p.Id,
                (a, p) => new { Assignment = a, p.Name });

        if (request.TraineeId.HasValue)
            query = query.Where(x => x.Assignment.TraineeId == request.TraineeId.Value);
        if (request.ActiveOnly)
            query = query.Where(x => x.Assignment.IsActive);

        var totalCount = await query.CountAsync(cancellationToken);

        // Project directly to the summary DTO so the (potentially large) jsonb snapshot is never loaded.
        var items = await query
            .OrderByDescending(x => x.Assignment.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NutritionAssignmentSummaryDto(
                x.Assignment.Id,
                x.Assignment.TraineeId,
                x.Assignment.PlanId,
                x.Assignment.PlanVersion,
                x.Name,
                x.Assignment.StartDate,
                x.Assignment.EndDate,
                x.Assignment.VisibilityMode,
                x.Assignment.HideMacroTargets,
                x.Assignment.DisableTraineeEditing,
                x.Assignment.IsActive))
            .ToListAsync(cancellationToken);

        return Result<NutritionAssignmentListDto>.Success(new NutritionAssignmentListDto(items, page, pageSize, totalCount));
    }
}
