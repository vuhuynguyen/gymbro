using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Queries.Handlers;

public sealed class GetNutritionPlanByIdHandler(INutritionPlanRepository repository)
    : IRequestHandler<GetNutritionPlanByIdQuery, Result<NutritionPlanDetailDto>>
{
    public async Task<Result<NutritionPlanDetailDto>> Handle(GetNutritionPlanByIdQuery request, CancellationToken cancellationToken)
    {
        // EF tenant filter scopes this to the caller's gym; a foreign plan id resolves to NotFound.
        var plan = await repository.Query()
            .AsNoTracking()
            .Include(p => p.Meals)
            .ThenInclude(m => m.Items)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result<NutritionPlanDetailDto>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var latestPublishedVersion = await repository.Query()
            .Where(p => p.TemplateId == plan.TemplateId && !p.IsDraft)
            .MaxAsync(p => (int?)p.Version, cancellationToken);

        return Result<NutritionPlanDetailDto>.Success(NutritionMapping.ToDetailDto(plan, latestPublishedVersion));
    }
}
