using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Application.Mapping;

namespace Modules.ExerciseModule.Application.Caching;

/// <summary>Exercise detail query used by the catalog cache service.</summary>
public sealed class ExerciseCatalogDetailReader(IExerciseRepository repository)
{
    public async Task<ExerciseDetailDto?> LoadAsync(Guid exerciseId, CancellationToken cancellationToken) =>
        await repository.Query()
            .AsNoTracking()
            .Where(x => x.Id == exerciseId && !x.IsDeleted)
            .Select(ExerciseMapping.ExerciseDetailProjection)
            .FirstOrDefaultAsync(cancellationToken);
}
