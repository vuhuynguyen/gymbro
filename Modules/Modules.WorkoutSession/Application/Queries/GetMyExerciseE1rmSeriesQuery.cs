using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

/// <summary>
/// SELF-SCOPED (no tenant context): the caller's own per-lift e1RM series + PR markers + strength summary
/// for one exercise (the strength drill-down behind the home sparkline). <see cref="From"/>/<see cref="To"/>
/// are optional local-day bounds (default: trailing 12 weeks). Computed across all of the caller's gyms via
/// <c>QueryOwnAcrossGyms(currentUser.UserId)</c> — never another trainee's data. Classified ImperativeGuarded
/// in TenantAuthorizationExemptions; an unknown/never-trained lift returns 200 with empty Points.
/// </summary>
public sealed record GetMyExerciseE1rmSeriesQuery(Guid ExerciseId, DateOnly? From, DateOnly? To)
    : IRequest<Result<ExerciseE1rmSeriesDto>>;
