using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Commands;

public sealed record EditSetCommand(
    Guid SessionId,
    Guid ExerciseId,
    Guid SetId,
    int? Reps,
    decimal? WeightKg,
    int? DurationSeconds,
    int? DistanceM,
    int? Rpe,
    int? RestSeconds,
    bool? IsCompleted,
    PerformedSetType? SetType,
    int? Calories = null,
    int? AvgHeartRate = null,
    int? Rounds = null) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
