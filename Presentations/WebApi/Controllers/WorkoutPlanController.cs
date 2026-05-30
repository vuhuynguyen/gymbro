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
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListWorkoutPlansQuery(search, page, pageSize), cancellationToken);

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

        return NoContent();
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

        var result = await mediator.Send(new ReplaceWorkoutPlanStructureCommand(id, workouts), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToFailureResult(this);
        }

        return NoContent();
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

    [HttpGet("assignments")]
    public async Task<IActionResult> ListAssignments(
        [FromQuery] Guid? traineeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListPlanAssignmentsQuery(traineeId, page, pageSize), cancellationToken);
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
}
