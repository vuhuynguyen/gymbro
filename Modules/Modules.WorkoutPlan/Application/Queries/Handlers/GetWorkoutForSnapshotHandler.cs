using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Application.Mapping;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class GetWorkoutForSnapshotHandler(IWorkoutPlanRepository repository, IMediator mediator)
    : IRequestHandler<GetWorkoutForSnapshotQuery, Result<PlanWorkoutDetailDto?>>
{
    public async Task<Result<PlanWorkoutDetailDto?>> Handle(
        GetWorkoutForSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var workout = await repository.GetWorkoutWithExercisesAsync(request.WorkoutId, cancellationToken);
        if (workout is null)
            return Result<PlanWorkoutDetailDto?>.Success(null);

        var exerciseIds = workout.Exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var namesResult = await mediator.Send(new ResolveExerciseNamesQuery(exerciseIds), cancellationToken);
        if (namesResult.IsFailure)
            return Result<PlanWorkoutDetailDto?>.Failure(namesResult.Error);

        var dto = WorkoutPlanMapping.ToPlanWorkoutDetailDto(workout, namesResult.Value!);
        return Result<PlanWorkoutDetailDto?>.Success(dto);
    }
}
