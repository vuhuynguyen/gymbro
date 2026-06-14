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
