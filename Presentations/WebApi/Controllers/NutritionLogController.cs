using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.NutritionModule.Application.Commands;
using WebApi.Http;
using WebApi.Requests.Nutrition;

namespace WebApi.Controllers;

/// <summary>
/// Tenant-scoped trainee nutrition logging. Every endpoint requires <c>X-Tenant-Id</c> (membership-validated by
/// <c>TenantResolutionMiddleware</c>) and <c>Permission.NutritionLogCreate</c> (held by Owner AND Client),
/// gated declaratively by <c>AuthorizationBehavior</c> — the exact mirror of how workout writes live on
/// <c>api/sessions</c> (<c>StartSessionCommand</c>). The handlers still scope every mutation to the caller's
/// own day (<c>currentUser.UserId</c>); a nutrition day is unique per (trainee, date) globally, so its tenant
/// is simply the gym that was active when the day was first created. Trainee READS and personal body metrics
/// stay self-scoped on <c>api/me/nutrition/*</c>.
/// </summary>
[ApiController]
[Route("api/nutrition/log")]
[Authorize]
public sealed class NutritionLogController(IMediator mediator) : ControllerBase
{
    [HttpPost("items/status")]
    public async Task<IActionResult> SetItemStatus([FromBody] SetNutritionItemStatusRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new SetNutritionItemStatusCommand(request.Date, request.ItemId, request.Status, request.Note), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(new { updated = true });
    }

    [HttpPost("items/substitute")]
    public async Task<IActionResult> SubstituteItem([FromBody] SubstituteNutritionItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new SubstituteNutritionItemCommand(request.Date, request.ItemId, request.FoodId, request.Quantity, request.Note), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(new { updated = true });
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddAdhocItem([FromBody] AddAdhocNutritionItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new AddAdhocNutritionItemCommand(
                request.Date, request.FoodId, request.Quantity, request.MealName, request.Note,
                request.CustomName, request.CustomKind, request.ServingLabel,
                request.EnergyKcal, request.ProteinG, request.CarbsG, request.FatG, request.FiberG,
                request.ClientItemId), ct);
        return result.IsFailure ? result.ToFailureResult(this) : StatusCode(201, result.Value);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid itemId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveNutritionItemCommand(date, itemId), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }
}
