using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.ExerciseModule.Application.Commands;

public record CreateExerciseCommand(
    string Name,
    string Description,
    string MuscleGroup,
    string? ImageUrl
) : IRequest<Result<Guid>>;