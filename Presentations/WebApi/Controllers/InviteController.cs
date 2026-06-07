using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Modules.UserModule.Application.Commands;
using Modules.UserModule.Application.Queries;
using WebApi.Requests.User;
using WebApi.Http;

namespace WebApi.Controllers;

[ApiController]
[Route("api/invites")]
[Authorize]
public class InviteController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Generate a shareable invite code for the active tenant. Owner only.
    /// Requires X-Tenant-Id header.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate()
    {
        var result = await mediator.Send(new GenerateInviteCommand());

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(new { code = result.Value });
    }

    /// <summary>
    /// List all invite codes for the active tenant. Owner only.
    /// Requires X-Tenant-Id header.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInvites()
    {
        var result = await mediator.Send(new GetTenantInvitesQuery());

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Revoke an active invite code. Owner only.
    /// Requires X-Tenant-Id header.
    /// </summary>
    [HttpDelete("{code}")]
    public async Task<IActionResult> RevokeInvite(string code)
    {
        var result = await mediator.Send(new RevokeInviteCommand(code));

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    /// <summary>
    /// Join a tenant using an invite code. Any authenticated user.
    /// Rate-limited per caller to throttle invite-code guessing.
    /// </summary>
    [HttpPost("join")]
    [EnableRateLimiting("tenant-join")]
    public async Task<IActionResult> Join(JoinTenantRequest request)
    {
        var result = await mediator.Send(new JoinTenantCommand(request.Code));

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(new { tenantId = result.Value });
    }
}
