using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Commands;

public sealed record PlanSetInput(
    PlanSetType SetType,
    int? TargetReps,
    decimal? TargetWeightKg,
    int? TargetRpe,
    int? TargetDurationSeconds,
    int RestSeconds,
    int Order);

public sealed record PlanWorkoutExerciseInput(Guid ExerciseId, int Order, IReadOnlyList<PlanSetInput> Sets);

public sealed record PlanWorkoutStructureInput(string Name, int Order, IReadOnlyList<PlanWorkoutExerciseInput> Exercises);

public sealed record ReplaceWorkoutPlanStructureCommand(
    Guid Id,
    IReadOnlyList<PlanWorkoutStructureInput> Workouts) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanUpdate;
}
