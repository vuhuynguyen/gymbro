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
    int Order,
    int? TargetDistanceM = null,
    int? TargetRounds = null);

public sealed record PlanWorkoutExerciseInput(Guid ExerciseId, int Order, IReadOnlyList<PlanSetInput> Sets, Guid? SupersetGroupId = null);

public sealed record PlanWorkoutStructureInput(string Name, int Order, IReadOnlyList<PlanWorkoutExerciseInput> Exercises);

// Carries metadata alongside the structure so a plan-builder save lands as a SINGLE new version.
// (Splitting metadata and structure across two version-forking PUTs makes the second one target a now-stale
// id → 409. See docs/BUSINESS_RULES.md "Workout Plan lifecycle".) Returns the new version id so the caller
// can re-point to the latest version for its next edit.
public sealed record ReplaceWorkoutPlanStructureCommand(
    Guid Id,
    string Name,
    string? Description,
    int? DurationWeeks,
    int? WorkoutsPerWeek,
    IReadOnlyList<PlanWorkoutStructureInput> Workouts) : IRequest<Result<Guid>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanUpdate;
}
