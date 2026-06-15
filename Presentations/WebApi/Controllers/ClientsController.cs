using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.WorkoutSessionModule.Application.Queries;
using WebApi.Http;

namespace WebApi.Controllers;

/// <summary>
/// The COACH progress surface: tenant-scoped, own-gym-only reads of a gym's clients. Every endpoint requires
/// <c>X-Tenant-Id</c>, is gated by <c>WorkoutLogViewAll</c> + <c>ResourceAccessGuard</c> in its handler, and
/// computes over TENANT-FILTERED sessions (EF tenant filter ON) — a client's cross-gym training is invisible
/// here by design (FEASIBILITY R2). Distinct from the self-scoped trainee surface on <c>api/me/*</c>.
/// </summary>
[ApiController]
[Route("api/clients")]
[Authorize]
public sealed class ClientsController(IMediator mediator) : ControllerBase
{
    /// <summary>The needs-attention roster for the active gym, sorted at-risk-first. 200 + empty Items when
    /// the gym has no members with sessions; "this gym only" is a UI caption, not a field.</summary>
    [HttpGet("progress/roster")]
    public async Task<IActionResult> Roster(CancellationToken ct)
    {
        var result = await mediator.Send(new GetClientRosterQuery(), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    /// <summary>One client's top-lift e1RM trends, built from this gym's sessions only. 403/404 when the
    /// trainee is not a member of the active tenant — never a silent rescope to self.</summary>
    [HttpGet("{traineeId:guid}/progress/strength")]
    public async Task<IActionResult> Strength(
        Guid traineeId, [FromQuery] int take = 6, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetClientStrengthQuery(traineeId, take), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    /// <summary>One client's acute-vs-chronic training load (7-day acute vs 28-day weekly-average), built from
    /// this gym's sessions only. The two raw volumes are returned SEPARATELY with a soft trend band — never an
    /// ACWR ratio. 200 + zeros when the client has no in-gym sessions in the window; 403/404 when the trainee
    /// is not a member of the active tenant — never a silent rescope to self.</summary>
    [HttpGet("{traineeId:guid}/progress/load")]
    public async Task<IActionResult> Load(Guid traineeId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetClientLoadQuery(traineeId), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }
}
