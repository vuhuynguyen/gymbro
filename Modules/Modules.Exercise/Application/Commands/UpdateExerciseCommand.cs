using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.ExerciseModule.Application.Commands;

public record UpdateExerciseCommand(
    Guid ExerciseId,
    string Name,
    string Description,
    string Type,
    string MovementType,
    string Difficulty,
    string Equipment,
    int? EstimatedCaloriesBurn,
    int? AverageDurationSeconds,
    string? ImageUrl,
    IReadOnlyList<ExerciseMuscleInput> Muscles,
    IReadOnlyList<string>? Instructions,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<ExerciseMediaInput>? Media,
    IReadOnlyList<string>? Warnings
) : IRequest<Result<Guid>>;
