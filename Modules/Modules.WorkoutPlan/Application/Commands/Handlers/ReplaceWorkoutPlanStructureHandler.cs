using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class ReplaceWorkoutPlanStructureHandler(
    IWorkoutPlanRepository repository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<ReplaceWorkoutPlanStructureCommand, Result>
{
    public async Task<Result> Handle(ReplaceWorkoutPlanStructureCommand request, CancellationToken cancellationToken)
    {
        var exerciseIds = request.Workouts
            .SelectMany(w => w.Exercises)
            .Select(e => e.ExerciseId)
            .Distinct()
            .ToList();

        var validation = await mediator.Send(new ValidateExerciseIdsQuery(exerciseIds), cancellationToken);
        if (validation.IsFailure)
            return validation;

        var mapped = request.Workouts
            .Select(w => (
                w.Name,
                w.Order,
                (IReadOnlyList<(Guid ExerciseId, int Order, IReadOnlyList<PlanWorkoutSetData> Sets)>)w.Exercises
                    .Select(e => (
                        e.ExerciseId,
                        e.Order,
                        (IReadOnlyList<PlanWorkoutSetData>)e.Sets
                            .Select(s => new PlanWorkoutSetData(
                                s.SetType,
                                s.TargetReps,
                                s.TargetWeightKg,
                                s.TargetRpe,
                                s.TargetDurationSeconds,
                                s.RestSeconds,
                                s.Order))
                            .ToList()))
                    .ToList()))
            .ToList();

        var current = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (current == null)
            return Result.Failure(NotFound("NotFound", "Plan not found."));

        var latest = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;
        var next = WorkoutPlan.CreateNewVersion(
            latest,
            currentUser.UserId,
            latest.Name,
            latest.Description,
            latest.DurationWeeks,
            latest.WorkoutsPerWeek);

        next.ReplaceStructure(mapped);
        await repository.AddAsync(next, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
