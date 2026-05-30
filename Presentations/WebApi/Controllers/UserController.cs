using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.UserModule.Application.Commands;
using Modules.UserModule.Application.Queries;
using WebApi.Requests.User;
using WebApi.Http;

namespace WebApi.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class UserController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateTenant(CreateTenantRequest request)
    {
        var result = await mediator.Send(new CreateTenantCommand(request.Name));

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return CreatedAtAction(nameof(GetMembers), new { tenantId = result.Value }, new { id = result.Value });
    }

    [HttpPost("invite")]
    public async Task<IActionResult> InviteUser(InviteUserRequest request)
    {
        var result = await mediator.Send(new InviteUserCommand(request.Email));

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(new { code = result.Value });
    }

    [HttpDelete("{tenantId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid tenantId, Guid userId)
    {
        var result = await mediator.Send(new RemoveMemberCommand(tenantId, userId));

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpDelete("{tenantId:guid}/leave")]
    public async Task<IActionResult> LeaveTenant(Guid tenantId)
    {
        var result = await mediator.Send(new LeaveTenantCommand(tenantId));

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTenants()
    {
        var result = await mediator.Send(new GetMyTenantsQuery());

        if (result.IsFailure)
            return BadRequest(result.Error.Message);

        return Ok(result.Value);
    }

    [HttpGet("{tenantId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid tenantId)
    {
        var result = await mediator.Send(new GetTenantMembersQuery(tenantId));

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }
}
