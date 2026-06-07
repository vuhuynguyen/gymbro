using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Application.Mapping;
using Modules.ExerciseModule.Application.Queries;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Caching;

/// <summary>Shared exercise-catalog search query used by the catalog cache service.</summary>
public sealed class ExerciseCatalogSearchReader(IExerciseRepository repository)
{
    public async Task<List<ExerciseDto>> LoadPageAsync(
        SearchExercisesQuery request,
        CancellationToken cancellationToken)
    {
        var query = repository.Query().AsNoTracking().Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.DefaultName.Contains(request.Search));

        if (!string.IsNullOrWhiteSpace(request.MuscleGroup)
            && Enum.TryParse<MuscleGroup>(request.MuscleGroup, true, out var muscle))
        {
            query = query.Where(x => x.Muscles.Any(m => m.Muscle == muscle));
        }

        if (!string.IsNullOrWhiteSpace(request.Type)
            && Enum.TryParse<ExerciseType>(request.Type, true, out var type))
        {
            query = query.Where(x => x.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(request.MovementType)
            && Enum.TryParse<MovementType>(request.MovementType, true, out var movementType))
        {
            query = query.Where(x => x.MovementType == movementType);
        }

        if (!string.IsNullOrWhiteSpace(request.Difficulty)
            && Enum.TryParse<DifficultyLevel>(request.Difficulty, true, out var difficulty))
        {
            query = query.Where(x => x.Difficulty == difficulty);
        }

        if (!string.IsNullOrWhiteSpace(request.Equipment)
            && Enum.TryParse<Equipment>(request.Equipment, true, out var equipment))
        {
            query = query.Where(x => x.Equipment == equipment);
        }

        return await query
            .OrderBy(x => x.DefaultName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ExerciseMapping.ExerciseDtoProjection)
            .ToListAsync(cancellationToken);
    }
}
