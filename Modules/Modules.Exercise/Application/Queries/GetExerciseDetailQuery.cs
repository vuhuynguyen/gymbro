using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.DTOs;

namespace Modules.ExerciseModule.Application.Queries;

public record GetExerciseByIdQuery(Guid Id)
    : IRequest<Result<ExerciseDetailDto>>;