using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Queries;
using WebApi.Requests.WorkoutPlan;
using WebApi.Http;

namespace WebApi.Controllers;

[ApiController]
[Route("api/workout-plans")]
[Authorize]
public sealed class WorkoutPlanController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool archived = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListWorkoutPlansQuery(search, page, pageSize, archived), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWorkoutPlanByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    // Returns the latest version in the template that {id} belongs to. The plan builder loads through this so
    // a stale (non-latest) version id self-heals to the editable latest version instead of 409-ing on save.
    [HttpGet("{id:guid}/latest")]
    public async Task<IActionResult> GetLatestById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLatestWorkoutPlanByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkoutPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateWorkoutPlanCommand(
                request.Name,
                request.Description,
                request.DurationWeeks,
                request.WorkoutsPerWeek),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWorkoutPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateWorkoutPlanCommand(
                id,
                request.Name,
                request.Description,
                request.DurationWeeks,
                request.WorkoutsPerWeek),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        // Editing forks a new version — return its id so the client can re-point to the latest.
        return Ok(new { id = result.Value });
    }

    [HttpPut("{id:guid}/structure")]
    public async Task<IActionResult> ReplaceStructure(
        Guid id,
        [FromBody] ReplaceWorkoutPlanStructureRequest request,
        CancellationToken cancellationToken)
    {
        var workouts = (request.Workouts ?? [])
            .Select(w => new PlanWorkoutStructureInput(
                w.Name,
                w.Order,
                (w.Exercises ?? [])
                    .Select(e => new PlanWorkoutExerciseInput(
                        e.ExerciseId,
                        e.Order,
                        (e.Sets ?? [])
                            .Select(s => new PlanSetInput(
                                s.SetType,
                                s.TargetReps,
                                s.TargetWeightKg,
                                s.TargetRpe,
                                s.TargetDurationSeconds,
                                s.RestSeconds,
                                s.Order))
                            .ToList()))
                    .ToList()))
            .ToList();

        var result = await mediator.Send(
            new ReplaceWorkoutPlanStructureCommand(
                id,
                request.Name,
                request.Description,
                request.DurationWeeks,
                request.WorkoutsPerWeek,
                workouts),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        // Editing forks a new version — return its id so the client can re-point to the latest.
        return Ok(new { id = result.Value });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteWorkoutPlanCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpPut("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetWorkoutPlanArchivedCommand(id, true), cancellationToken);

        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpPut("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetWorkoutPlanArchivedCommand(id, false), cancellationToken);

        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> ListAssignments(
        [FromQuery] Guid? traineeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListPlanAssignmentsQuery(traineeId, page, pageSize, activeOnly), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(result.Value);
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] CreatePlanAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new CreatePlanAssignmentCommand(
                request.TraineeId,
                request.PlanId,
                request.StartDate,
                request.FrequencyDaysPerWeek,
                request.VisibilityMode,
                request.HideExercises,
                request.HideSetsReps,
                request.HideFutureWorkouts,
                request.DisableTraineeEditing,
                request.SnapshotJson),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return CreatedAtAction(nameof(ListAssignments), new { id = result.Value }, new { id = result.Value });
    }

    [HttpPut("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> UpdateAssignment(
        Guid assignmentId,
        [FromBody] UpdatePlanAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new UpdatePlanAssignmentCommand(
                assignmentId,
                request.StartDate,
                request.FrequencyDaysPerWeek,
                request.VisibilityMode,
                request.HideExercises,
                request.HideSetsReps,
                request.HideFutureWorkouts,
                request.DisableTraineeEditing),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpDelete("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> DeleteAssignment(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new DeletePlanAssignmentCommand(assignmentId), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
    }

    [HttpPut("assignments/{assignmentId:guid}/apply-latest")]
    public async Task<IActionResult> ApplyLatestVersion(
        Guid assignmentId,
        [FromBody] UpdatePlanAssignmentToLatestVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new UpdatePlanAssignmentToLatestVersionCommand(assignmentId, request.SnapshotJson),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return Ok(new { updated = result.Value });
    }

    [HttpPut("assignments/{assignmentId:guid}/pause")]
    public async Task<IActionResult> PauseAssignment(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new SetPlanAssignmentActiveCommand(assignmentId, false), cancellationToken);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }

    [HttpPut("assignments/{assignmentId:guid}/resume")]
    public async Task<IActionResult> ResumeAssignment(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new SetPlanAssignmentActiveCommand(assignmentId, true), cancellationToken);
        return result.IsFailure ? result.ToFailureResult(this) : NoContent();
    }
}
