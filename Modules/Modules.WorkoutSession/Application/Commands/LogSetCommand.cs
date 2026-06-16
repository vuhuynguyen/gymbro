using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Commands;

public sealed record LogSetCommand(
    Guid SessionId,
    Guid ExerciseId,
    Guid? PlanSetId,
    int SetNumber,
    PerformedSetType SetType,
    int? Reps,
    decimal? WeightKg,
    int? DurationSeconds,
    int? DistanceM,
    int? Rpe,
    int? RestSeconds,
    bool IsCompleted,
    int? Calories = null,
    int? AvgHeartRate = null,
    int? Rounds = null,
    decimal? InclinePercent = null,
    decimal? SpeedKph = null,
    int? Level = null,
    Guid? ParentSetId = null) : IRequest<Result<PerformedSetDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
