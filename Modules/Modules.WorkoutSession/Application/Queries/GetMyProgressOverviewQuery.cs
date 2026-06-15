using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

// The single-call trainee Progress home (api/me/progress/overview): this-week adherence, 12-week
// consistency, top-lift strength direction, and a PR teaser — computed across all of the caller's gyms.
public sealed record GetMyProgressOverviewQuery : IRequest<Result<ProgressOverviewDto>>;
