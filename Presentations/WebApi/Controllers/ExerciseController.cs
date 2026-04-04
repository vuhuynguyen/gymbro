using MediatR;
using Microsoft.AspNetCore.Mvc;
using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Application.Queries;
using WebApi.Requests.Exercise;

namespace WebApi.Controllers;

[ApiController]
[Route("api/exercises")]
public class ExerciseController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateExerciseRequest request)
    {
        var command = new CreateExerciseCommand(
            request.Name,
            request.Description,
            request.MuscleGroup,
            request.ImageUrl
        );

        var result = await mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(result.Error.Message);

        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await mediator.Send(new GetExerciseByIdQuery(id));

        if (result.IsFailure)
            return NotFound(result.Error.Message);

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchExercisesQuery query)
    {
        var result = await mediator.Send(query);

        if (result.IsFailure)
            return BadRequest(result.Error.Message);

        return Ok(result.Value);
    }
}