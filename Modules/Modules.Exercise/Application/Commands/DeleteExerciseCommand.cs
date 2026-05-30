using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.ExerciseModule.Application.Commands;

public record DeleteExerciseCommand(Guid ExerciseId) : IRequest<Result>;
