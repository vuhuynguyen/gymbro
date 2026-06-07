using BuildingBlocks.Application.Abstractions;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Abstractions;

public interface IExerciseRepository : IRepository<Exercise>
{
    Task<Exercise?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
}