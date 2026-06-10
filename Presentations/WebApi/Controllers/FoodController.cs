using BuildingBlocks.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.FoodModule.Application.Commands;
using Modules.FoodModule.Application.Queries;
using WebApi.Http;
using WebApi.Requests.Nutrition;

namespace WebApi.Controllers;

/// <summary>
/// Food / supplement catalog. Reads are available to any member with PlanView (handler-gated, admin too);
/// global writes are platform-admin-only; an Owner can add tenant-custom foods. Mirrors ExerciseController.
/// </summary>
[ApiController]
[Route("api/foods")]
[Authorize]
public sealed class FoodController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchFoodsQuery query, CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetFoodByIdQuery(id), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Create([FromBody] FoodRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateFoodCommand(ToInput(request)), ct);
        return result.IsFailure
            ? result.ToFailureResult(this)
            : CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpPost("custom")]
    public async Task<IActionResult> CreateCustom([FromBody] FoodRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateCustomFoodCommand(ToInput(request)), ct);
        return result.IsFailure
            ? result.ToFailureResult(this)
            : CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] FoodRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateFoodCommand(id, ToInput(request)), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteFoodCommand(id), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    private static FoodInput ToInput(FoodRequest r) => new(
        r.Name, r.Kind, r.ServingLabel, r.ServingSizeGrams,
        r.EnergyKcal, r.ProteinG, r.CarbsG, r.FatG, r.FiberG, r.Brand);
}
