using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

public sealed record GetWorkoutForSnapshotQuery(Guid WorkoutId)
    : IRequest<Result<PlanWorkoutDetailDto?>>;
