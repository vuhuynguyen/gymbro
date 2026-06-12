using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Queries;
using WebApi.Http;
using WebApi.Requests.Nutrition;

namespace WebApi.Controllers;

/// <summary>
/// Tenant-scoped nutrition surface: coach plan authoring + assignment, and the coach's read of a gym
/// client's nutrition days. Trainee logging lives on the self-scoped <c>api/me/nutrition/*</c> surface.
/// Mirrors WorkoutPlanController.
/// </summary>
[ApiController]
[Route("api/nutrition")]
[Authorize]
public sealed class NutritionController(IMediator mediator) : ControllerBase
{
    // ── Plans ─────────────────────────────────────────────────────────────

    [HttpGet("plans")]
    public async Task<IActionResult> ListPlans(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListNutritionPlansQuery(search, page, pageSize), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("plans/{id:guid}")]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNutritionPlanByIdQuery(id), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateNutritionPlanRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateNutritionPlanCommand(request.Name, request.Description), ct);
        return result.IsFailure
            ? result.ToFailureResult(this)
            : CreatedAtAction(nameof(GetPlan), new { id = result.Value }, result.Value);
    }

    [HttpPut("plans/{id:guid}/structure")]
    public async Task<IActionResult> ReplaceStructure(Guid id, [FromBody] ReplaceNutritionPlanStructureRequest request, CancellationToken ct)
    {
        var meals = request.Meals
            .Select(m => new NutritionPlanMealInput(
                m.Name, m.Order, m.ScheduledTime, m.DayApplicability,
                m.Items.Select(i => new NutritionPlanItemInput(i.FoodId, i.Order, i.Quantity)).ToList()))
            .ToList();

        var result = await mediator.Send(
            new ReplaceNutritionPlanStructureCommand(id, request.Name, request.Description, meals), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(new { id = result.Value });
    }

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteNutritionPlanCommand(id), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    // ── Assignments ───────────────────────────────────────────────────────

    [HttpGet("assignments")]
    public async Task<IActionResult> ListAssignments(
        [FromQuery] Guid? traineeId, [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListNutritionAssignmentsQuery(traineeId, activeOnly, page, pageSize), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateNutritionAssignmentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateNutritionAssignmentCommand(
            request.TraineeId, request.PlanId, request.StartDate, request.EndDate,
            request.VisibilityMode, request.HideMacroTargets, request.DisableTraineeEditing), ct);
        return result.IsFailure
            ? result.ToFailureResult(this)
            : CreatedAtAction(nameof(ListAssignments), new { id = result.Value }, new { id = result.Value });
    }

    // ── Coach client-log reads (adherence) ────────────────────────────────

    [HttpGet("logs")]
    public async Task<IActionResult> ListTraineeDays(
        [FromQuery] Guid traineeId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListTraineeNutritionDaysQuery(traineeId, from, to, page, pageSize), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }

    [HttpGet("logs/{date}")]
    public async Task<IActionResult> GetTraineeDay(DateOnly date, [FromQuery] Guid traineeId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTraineeNutritionDayQuery(traineeId, date), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(result.Value);
    }
}
