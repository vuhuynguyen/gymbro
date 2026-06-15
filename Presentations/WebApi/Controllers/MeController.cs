using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Queries;
using Modules.UserModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Entities;
using WebApi.Http;
using WebApi.Requests.Nutrition;

namespace WebApi.Controllers;

/// <summary>
/// The unified personal training experience. Every endpoint is self-scoped to the authenticated user and
/// aggregates across all gyms they belong to — no <c>X-Tenant-Id</c> is required or consulted. Coach/owner
/// views of a gym's members stay on the tenant-scoped <c>api/sessions</c> endpoints.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IMediator mediator) : ControllerBase
{
    [HttpGet("sessions")]
    public async Task<IActionResult> History(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] SessionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMyWorkoutHistoryQuery(from, to, status, page, pageSize), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> SessionDetail(Guid sessionId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyWorkoutSessionByIdQuery(sessionId), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyPersonalRecordsQuery(), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("progress")]
    public async Task<IActionResult> Progress(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyProgressQuery(), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    /// <summary>The single-call trainee Progress home: adherence, consistency, top-lift direction, PR teaser.
    /// Optional <paramref name="weeks"/> selects the consistency/heatmap/top-lift window (clamped to [4, 52],
    /// default 12); the This-Week hero and goal are unaffected.</summary>
    [HttpGet("progress/overview")]
    public async Task<IActionResult> ProgressOverview([FromQuery] int? weeks, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyProgressOverviewQuery(weeks), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    /// <summary>The caller's own per-lift e1RM series + PR markers + strength summary (the strength
    /// drill-down behind the home sparkline). Self-scoped, cross-gym; 200 + empty Points for an unknown lift.</summary>
    [HttpGet("exercises/{exerciseId:guid}/e1rm-series")]
    public async Task<IActionResult> ExerciseE1rmSeries(
        Guid exerciseId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyExerciseE1rmSeriesQuery(exerciseId, from, to), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    /// <summary>The caller's own body-metric trend (latest-per-day) for one metric type (e.g. weight).
    /// Self-scoped; case-insensitive type; 200 + empty Points when nothing is logged in range.</summary>
    [HttpGet("progress/metrics/series")]
    public async Task<IActionResult> MetricSeries(
        [FromQuery] string type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyMetricSeriesQuery(type, from, to), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    /// <summary>The caller's own nutrition-plan adherence trend (default trailing 4 weeks) for the Progress
    /// Body section. Self-scoped, cross-gym; 200 + HasPlan=false/empty when the caller has never had a plan.</summary>
    [HttpGet("progress/nutrition-adherence")]
    public async Task<IActionResult> NutritionAdherence(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyNutritionAdherenceQuery(from, to), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    // ── Nutrition reads + personal metrics (self-scoped, cross-gym) ───────
    // Trainee nutrition WRITES (item status/substitute/ad-hoc/remove) are tenant-scoped and live on
    // NutritionLogController (api/nutrition/log). Reads below and the personal metric series stay self-scoped.

    [HttpGet("nutrition/today")]
    public async Task<IActionResult> NutritionToday(
        [FromQuery] DateOnly? date, [FromQuery] string? timezone, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyNutritionTodayQuery(date, timezone), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("nutrition/days")]
    public async Task<IActionResult> NutritionHistory(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMyNutritionHistoryQuery(from, to, page, pageSize), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("nutrition/days/{date}")]
    public async Task<IActionResult> NutritionDay(DateOnly date, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyNutritionDayQuery(date), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("nutrition/metrics")]
    public async Task<IActionResult> NutritionMetrics([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyNutritionMetricsQuery(date), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpPost("nutrition/metrics")]
    public async Task<IActionResult> LogNutritionMetric([FromBody] LogMetricEntryRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new LogMetricEntryCommand(request.Type, request.Value, request.Unit, request.LocalDate), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(new { logged = true });
    }

    /// <summary>Sets the caller's IANA time-zone — the authoritative anchor for their day/week boundaries.</summary>
    [HttpPut("timezone")]
    public async Task<IActionResult> SetTimeZone([FromBody] SetMyTimeZoneCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }
}
