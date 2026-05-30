using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class GetWorkoutForSnapshotHandler(IWorkoutPlanRepository repository)
    : IRequestHandler<GetWorkoutForSnapshotQuery, Result<PlanWorkoutDetailDto?>>
{
    public async Task<Result<PlanWorkoutDetailDto?>> Handle(
        GetWorkoutForSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var workout = await repository.GetWorkoutForSnapshotAsync(request.WorkoutId, cancellationToken);
        return Result<PlanWorkoutDetailDto?>.Success(workout);
    }
}
