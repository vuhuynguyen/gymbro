using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Entities;
using WebApi.Http;

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
}
