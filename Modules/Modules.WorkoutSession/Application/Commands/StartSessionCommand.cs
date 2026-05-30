using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Commands;

public sealed record StartSessionCommand(
    SessionSource Source,
    Guid? PlanAssignmentId,
    Guid? PlannedWorkoutId,
    string? ClientTimezone,
    decimal? BodyweightKg) : IRequest<Result<SessionStartResultDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
