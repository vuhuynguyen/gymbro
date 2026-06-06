using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Authorization;
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

        if (current.IsArchived)
            return Result.Failure(Conflict("Conflict", "Unarchive the plan before editing it."));

        var latest = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;

        // Edits must target the latest version; editing an older one would silently fork off the newest.
        if (current.Id != latest.Id)
            return Result.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and edit the latest version."));

        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(latest, currentUser);
        if (authorCheck.IsFailure)
            return authorCheck;

        var next = WorkoutPlan.CreateNewVersion(
            latest,
            currentUser.UserId,
            latest.Name,
            latest.Description,
            latest.DurationWeeks,
            latest.WorkoutsPerWeek);

        next.ReplaceStructure(mapped);
        await repository.AddAsync(next, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent edit created the same version; caller should reload and retry.
            return Result.Failure(Conflict("Conflict", "This plan was modified concurrently. Refresh and try again."));
        }

        return Result.Success();
    }
}
