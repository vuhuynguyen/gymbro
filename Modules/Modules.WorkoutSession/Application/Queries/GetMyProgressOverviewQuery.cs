using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

// The single-call trainee Progress home (api/me/progress/overview): this-week adherence, consistency,
// top-lift strength direction, and a PR teaser — computed across all of the caller's gyms.
//
// <see cref="Weeks"/> is the optional user-selectable window for the consistency window, the consistency
// heatmap span, and the strength top-lift gathering. The handler clamps it to [4, 52] (default 12 when
// null). It does NOT move the This-Week hero/goal, the trailing-4-week strength baseline, or the 3-exposure
// stall — those stay fixed regardless of the selected window.
public sealed record GetMyProgressOverviewQuery(int? Weeks = null) : IRequest<Result<ProgressOverviewDto>>;
