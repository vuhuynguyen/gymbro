using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Commands;

public sealed record CompleteSessionResultDto(
    Guid SessionId,
    int? DurationSeconds,
    int TotalSets,
    int TotalExercises,
    DateTimeOffset CompletedAt);

public sealed record CompleteSessionCommand(
    Guid SessionId,
    int? RpeOverall,
    string? Notes,
    DateTimeOffset? CompletedAt) : IRequest<Result<CompleteSessionResultDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
