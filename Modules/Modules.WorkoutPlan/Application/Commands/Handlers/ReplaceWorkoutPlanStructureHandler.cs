using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Authorization;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class ReplaceWorkoutPlanStructureHandler(
    IWorkoutPlanRepository repository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<ReplaceWorkoutPlanStructureCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReplaceWorkoutPlanStructureCommand request, CancellationToken cancellationToken)
    {
        var exerciseIds = request.Workouts
            .SelectMany(w => w.Exercises)
            .Select(e => e.ExerciseId)
            .Distinct()
            .ToList();

        var validation = await mediator.Send(new ValidateExerciseIdsQuery(exerciseIds), cancellationToken);
        if (validation.IsFailure)
            return Result<Guid>.Failure(validation.Error);

        var mapped = request.Workouts
            .Select(w => (
                w.Name,
                w.Order,
                (IReadOnlyList<(Guid ExerciseId, int Order, IReadOnlyList<PlanWorkoutSetData> Sets, Guid? SupersetGroupId)>)w.Exercises
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
                                s.Order,
                                s.TargetDistanceM,
                                s.TargetRounds))
                            .ToList(),
                        e.SupersetGroupId))
                    .ToList()))
            .ToList();

        var current = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (current == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Plan not found."));

        if (current.IsArchived)
            return Result<Guid>.Failure(Conflict("Conflict", "Unarchive the plan before editing it."));

        var head = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;

        // Edits must target the latest version; editing an older one would silently fork off the newest.
        if (current.Id != head.Id)
            return Result<Guid>.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and edit the latest version."));

        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(head, currentUser);
        if (authorCheck.IsFailure)
            return Result<Guid>.Failure(authorCheck.Error);

        // Edits never bump the published version: metadata + structure land together on the single draft head.
        // Replacing an existing draft keeps its version number (and drops the old draft row); the first edit after
        // a publish forks a new draft one above the latest published version. The structure is rebuilt as a fresh
        // untracked graph, so one AddAsync persists it.
        var draftVersion = head.IsDraft ? head.Version : head.Version + 1;
        var next = WorkoutPlan.CreateDraft(
            head,
            currentUser.UserId,
            draftVersion,
            request.Name,
            request.Description,
            request.DurationWeeks,
            request.WorkoutsPerWeek);

        next.ReplaceStructure(mapped);

        if (head.IsDraft)
            repository.Remove(head);
        await repository.AddAsync(next, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent edit created the same version; caller should reload and retry.
            return Result<Guid>.Failure(Conflict("Conflict", "This plan was modified concurrently. Refresh and try again."));
        }

        // Return the draft head's id so the caller can re-point to the latest version for its next edit.
        return Result<Guid>.Success(next.Id);
    }
}
