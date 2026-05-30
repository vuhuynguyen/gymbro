using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class GetPlanAssignmentByIdHandler(IPlanAssignmentRepository repository)
    : IRequestHandler<GetPlanAssignmentByIdQuery, Result<PlanAssignmentForSessionDto>>
{
    public async Task<Result<PlanAssignmentForSessionDto>> Handle(
        GetPlanAssignmentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var assignment = await repository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (assignment == null)
            return Result<PlanAssignmentForSessionDto>.Failure(NotFound("NotFound", "Plan assignment not found."));

        return Result<PlanAssignmentForSessionDto>.Success(
            new PlanAssignmentForSessionDto(assignment.Id, assignment.TraineeId, assignment.VisibilityMode));
    }
}
