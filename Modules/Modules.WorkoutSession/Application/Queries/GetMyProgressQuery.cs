using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

// Unified personal progress/analytics (weekly volume + frequency) across all of the caller's gyms.
public sealed record GetMyProgressQuery : IRequest<Result<ProgressDto>>;
