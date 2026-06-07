using BuildingBlocks.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Application.Queries;
using WebApi.Requests.Exercise;

namespace WebApi.Controllers;

[ApiController]
[Route("api/exercises")]
[Authorize]
public class ExerciseController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Create(CreateExerciseRequest request)
    {
        var muscles = MapMuscles(request.Muscles, request.MuscleGroup);

        var media = MapMedia(request.Media);
        var command = new CreateExerciseCommand(
            request.Name,
            request.Description,
            request.Type,
            request.MovementType,
            request.Difficulty,
            request.Equipment,
            request.EstimatedCaloriesBurn,
            request.AverageDurationSeconds,
            request.ImageUrl,
            muscles,
            request.Instructions,
            request.Tags,
            media,
            request.Warnings);

        var result = await mediator.Send(command);

        if (result.IsFailure)
            return MapExerciseCommandFailure(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await mediator.Send(new GetExerciseByIdQuery(id));

        if (result.IsFailure)
            return MapExerciseQueryFailure(result);

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchExercisesQuery query)
    {
        var result = await mediator.Send(query);

        if (result.IsFailure)
            return MapExerciseQueryFailure(result);

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Update(Guid id, UpdateExerciseRequest request)
    {
        var muscles = MapMuscles(request.Muscles, request.MuscleGroup);

        var media = MapMedia(request.Media);
        var command = new UpdateExerciseCommand(
            id,
            request.Name,
            request.Description,
            request.Type,
            request.MovementType,
            request.Difficulty,
            request.Equipment,
            request.EstimatedCaloriesBurn,
            request.AverageDurationSeconds,
            request.ImageUrl,
            muscles,
            request.Instructions,
            request.Tags,
            media,
            request.Warnings);

        var result = await mediator.Send(command);

        if (result.IsFailure)
            return MapExerciseCommandFailure(result);

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await mediator.Send(new DeleteExerciseCommand(id));

        if (result.IsFailure)
            return MapExerciseCommandFailure(result);

        return NoContent();
    }

    private IActionResult MapExerciseCommandFailure(
        BuildingBlocks.Shared.Results.Result<Guid> result)
    {
        if (result.Error.Code == "Conflict") return Conflict(result.Error.Message);
        if (result.Error.Code == "NotFound") return NotFound(result.Error.Message);
        if (IsForbiddenExerciseCode(result.Error.Code))
            return StatusCode(StatusCodes.Status403Forbidden, result.Error.Message);
        return BadRequest(result.Error.Message);
    }

    private IActionResult MapExerciseCommandFailure(BuildingBlocks.Shared.Results.Result result)
    {
        if (result.Error.Code == "NotFound") return NotFound(result.Error.Message);
        if (IsForbiddenExerciseCode(result.Error.Code))
            return StatusCode(StatusCodes.Status403Forbidden, result.Error.Message);
        return BadRequest(result.Error.Message);
    }

    private IActionResult MapExerciseQueryFailure<TResult>(
        BuildingBlocks.Shared.Results.Result<TResult> result)
    {
        if (result.Error.Code == "NotFound") return NotFound(result.Error.Message);
        if (IsForbiddenExerciseCode(result.Error.Code))
            return StatusCode(StatusCodes.Status403Forbidden, result.Error.Message);
        if (result.Error.Code == "Validation") return BadRequest(result.Error.Message);
        return BadRequest(result.Error.Message);
    }

    private static bool IsForbiddenExerciseCode(string code) =>
        code is "AdminOnly" or "Unauthorized"
        || code.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ExerciseMuscleInput> MapMuscles(
        List<ExerciseMuscleRequest>? muscles,
        string? legacyMuscleGroup)
    {
        if (muscles is { Count: > 0 })
        {
            return muscles
                .Select(m => new ExerciseMuscleInput(m.Muscle, m.IsPrimary))
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(legacyMuscleGroup))
            return Array.Empty<ExerciseMuscleInput>();

        return [new ExerciseMuscleInput(legacyMuscleGroup.Trim(), true)];
    }

    private static IReadOnlyList<ExerciseMediaInput>? MapMedia(List<ExerciseMediaItemRequest>? media)
    {
        if (media == null)
            return null;

        return media
            .Select(m => new ExerciseMediaInput(m.Url ?? string.Empty, m.Type ?? "Image"))
            .ToList();
    }
}