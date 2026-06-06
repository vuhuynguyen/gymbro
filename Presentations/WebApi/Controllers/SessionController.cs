using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Entities;
using WebApi.Http;
using WebApi.Requests.Session;

namespace WebApi.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public sealed class SessionController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartSessionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new StartSessionCommand(
            request.Source,
            request.PlanAssignmentId,
            request.PlannedWorkoutId,
            request.ClientTimezone,
            request.BodyweightKg), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return CreatedAtAction(nameof(GetById), new { sessionId = result.Value.SessionId }, result.Value);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await mediator.Send(new GetActiveSessionQuery(), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        if (result.Value == null)
            return NoContent();

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? traineeId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] SessionStatus? status,
        [FromQuery] Guid? planAssignmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListSessionsQuery(traineeId, from, to, status, planAssignmentId, page, pageSize), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return Ok(result.Value);
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetById(Guid sessionId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSessionByIdQuery(sessionId), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return Ok(result.Value);
    }

    [HttpPost("{sessionId:guid}/exercises")]
    public async Task<IActionResult> AddExercise(
        Guid sessionId,
        [FromBody] AddExerciseRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new AddPerformedExerciseCommand(
            sessionId,
            request.ExerciseId,
            request.PlanWorkoutExerciseId,
            request.Order,
            request.Notes), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return StatusCode(201, result.Value);
    }

    [HttpPut("{sessionId:guid}/exercises/{exerciseId:guid}")]
    public async Task<IActionResult> UpdateExercise(
        Guid sessionId,
        Guid exerciseId,
        [FromBody] UpdateExerciseRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new UpdatePerformedExerciseCommand(
            sessionId,
            exerciseId,
            request.Action,
            request.SubstituteExerciseId,
            request.Notes), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return Ok(new { updated = true });
    }

    [HttpPost("{sessionId:guid}/exercises/{exerciseId:guid}/sets")]
    public async Task<IActionResult> LogSet(
        Guid sessionId,
        Guid exerciseId,
        [FromBody] LogSetRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new LogSetCommand(
            sessionId,
            exerciseId,
            request.PlanSetId,
            request.SetNumber,
            request.SetType,
            request.Reps,
            request.WeightKg,
            request.DurationSeconds,
            request.DistanceM,
            request.Rpe,
            request.RestSeconds,
            request.IsCompleted), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return StatusCode(201, result.Value);
    }

    [HttpPut("{sessionId:guid}/exercises/{exerciseId:guid}/sets/{setId:guid}")]
    public async Task<IActionResult> EditSet(
        Guid sessionId,
        Guid exerciseId,
        Guid setId,
        [FromBody] EditSetRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new EditSetCommand(
            sessionId,
            exerciseId,
            setId,
            request.Reps,
            request.WeightKg,
            request.DurationSeconds,
            request.DistanceM,
            request.Rpe,
            request.RestSeconds,
            request.IsCompleted,
            request.SetType), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return Ok(new { updated = true });
    }

    [HttpDelete("{sessionId:guid}/exercises/{exerciseId:guid}/sets/{setId:guid}")]
    public async Task<IActionResult> DeleteSet(
        Guid sessionId,
        Guid exerciseId,
        Guid setId,
        CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteSetCommand(sessionId, exerciseId, setId), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return NoContent();
    }

    [HttpPost("{sessionId:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid sessionId,
        [FromBody] CompleteSessionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new CompleteSessionCommand(
            sessionId,
            request.RpeOverall,
            request.Notes,
            request.CompletedAt), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return Ok(result.Value);
    }

    [HttpPost("{sessionId:guid}/abandon")]
    public async Task<IActionResult> Abandon(
        Guid sessionId,
        [FromBody] AbandonSessionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new AbandonSessionCommand(sessionId, request.Notes), ct);

        if (result.IsFailure)
            return result.ToFailureResult(this);

        return Ok(new { sessionId, status = "Abandoned" });
    }

}
