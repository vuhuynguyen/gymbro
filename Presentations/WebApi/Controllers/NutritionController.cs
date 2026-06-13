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
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] bool archived = false, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListNutritionPlansQuery(search, page, pageSize, archived), ct);
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

    /// <summary>Publishes the plan's draft head — the only action that advances the assignable/published version.</summary>
    [HttpPut("plans/{id:guid}/publish")]
    public async Task<IActionResult> PublishPlan(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new PublishNutritionPlanCommand(id), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(new { version = result.Value });
    }

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteNutritionPlanCommand(id), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpPut("plans/{id:guid}/archive")]
    public async Task<IActionResult> ArchivePlan(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new SetNutritionPlanArchivedCommand(id, true), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpPut("plans/{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchivePlan(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new SetNutritionPlanArchivedCommand(id, false), ct);
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

    [HttpPut("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> UpdateAssignment(
        Guid assignmentId, [FromBody] UpdateNutritionAssignmentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateNutritionAssignmentCommand(
            assignmentId, request.StartDate, request.EndDate,
            request.VisibilityMode, request.HideMacroTargets, request.DisableTraineeEditing), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpDelete("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid assignmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteNutritionAssignmentCommand(assignmentId), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    /// <summary>Re-points the assignment to the plan's latest published version (rebuilds the pinned snapshot).</summary>
    [HttpPut("assignments/{assignmentId:guid}/apply-latest")]
    public async Task<IActionResult> ApplyLatestVersion(Guid assignmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateNutritionAssignmentToLatestVersionCommand(assignmentId), ct);
        return result.IsFailure ? result.ToFailureResult(this) : Ok(new { updated = result.Value });
    }

    [HttpPut("assignments/{assignmentId:guid}/pause")]
    public async Task<IActionResult> PauseAssignment(Guid assignmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new SetNutritionAssignmentActiveCommand(assignmentId, false), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpPut("assignments/{assignmentId:guid}/resume")]
    public async Task<IActionResult> ResumeAssignment(Guid assignmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new SetNutritionAssignmentActiveCommand(assignmentId, true), ct);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
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
