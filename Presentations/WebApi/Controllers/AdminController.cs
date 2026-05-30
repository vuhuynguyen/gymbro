using BuildingBlocks.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.UserModule.Application.Admin.Commands;
using Modules.UserModule.Application.Admin.Queries;
using Modules.IdentityModule.Application.Commands;
using WebApi.Requests.Admin;
using WebApi.Http;

namespace WebApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
public class AdminController(IMediator mediator) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await mediator.Send(new AdminGetTenantsQuery(page, pageSize));
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    [HttpDelete("tenants/{tenantId:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid tenantId)
    {
        var result = await mediator.Send(new AdminDeleteTenantCommand(tenantId));
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpGet("tenants/{tenantId:guid}/members")]
    public async Task<IActionResult> GetTenantMembers(Guid tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await mediator.Send(new AdminGetTenantMembersQuery(tenantId, page, pageSize));
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    [HttpDelete("tenants/{tenantId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid tenantId, Guid userId)
    {
        var result = await mediator.Send(new AdminRemoveMemberCommand(tenantId, userId));
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await mediator.Send(new AdminGetUsersQuery(page, pageSize));
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var result = await mediator.Send(new AdminDeleteUserCommand(userId));
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpPost("users/promote")]
    public async Task<IActionResult> PromoteUser(PromoteUserRequest request)
    {
        var result = await mediator.Send(new PromoteUserToAdminCommand(request.Email, request.IsAdmin));
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }
}
