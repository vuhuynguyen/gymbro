using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class GetLatestWorkoutPlanByIdHandler(
    IWorkoutPlanRepository repository,
    IMediator mediator)
    : IRequestHandler<GetLatestWorkoutPlanByIdQuery, Result<WorkoutPlanDetailDto>>
{
    public async Task<Result<WorkoutPlanDetailDto>> Handle(
        GetLatestWorkoutPlanByIdQuery request,
        CancellationToken cancellationToken)
    {
        // Resolve the template from the supplied (possibly stale) version id. The EF tenant/soft-delete
        // filters apply, so a cross-tenant or deleted id resolves to nothing → NotFound.
        var plan = await repository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result<WorkoutPlanDetailDto>.Failure(NotFound("NotFound", "Plan not found."));

        var latest = await repository.GetLatestVersionInTemplateAsync(plan.TemplateId, cancellationToken);
        var targetId = latest?.Id ?? plan.Id;

        // Delegate to the by-id read so authorization, trainee redaction and exercise-name enrichment all
        // stay in exactly one place.
        return await mediator.Send(new GetWorkoutPlanByIdQuery(targetId), cancellationToken);
    }
}
