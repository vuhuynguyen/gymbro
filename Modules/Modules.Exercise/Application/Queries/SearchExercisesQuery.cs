using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.DTOs;

namespace Modules.ExerciseModule.Application.Queries;

public record SearchExercisesQuery(
    string? Search,
    string? MuscleGroup,
    string? Type,
    string? MovementType,
    string? Difficulty,
    string? Equipment,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<List<ExerciseDto>>>;
