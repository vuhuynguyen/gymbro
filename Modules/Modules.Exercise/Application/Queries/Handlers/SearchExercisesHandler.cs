using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public class SearchExercisesHandler(IExerciseRepository repository)
    : IRequestHandler<SearchExercisesQuery, Result<List<ExerciseDto>>>
{
    public async Task<Result<List<ExerciseDto>>> Handle(
        SearchExercisesQuery request,
        CancellationToken cancellationToken)
    {
        var query = repository.Query().AsNoTracking();

        query = query.Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x => x.DefaultName.Contains(request.Search));
        }

        if (!string.IsNullOrWhiteSpace(request.MuscleGroup)
            && Enum.TryParse<MuscleGroup>(request.MuscleGroup, out var muscle))
        {
            query = query.Where(x => x.MuscleGroup == muscle);
        }

        var datasource = await query
            .OrderBy(x => x.DefaultName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ExerciseDto
            {
                Id = x.Id,
                Name = x.DefaultName,
                MuscleGroup = x.MuscleGroup.ToString(),
                ImageUrl = x.ImageUrl
            })
            .ToListAsync(cancellationToken);
        
        return Result<List<ExerciseDto>>.Success(datasource);
    }
}