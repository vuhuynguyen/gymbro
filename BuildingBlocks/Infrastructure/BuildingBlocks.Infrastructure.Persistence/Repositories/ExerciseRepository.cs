using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public class ExerciseRepository(AppDbContext context) : Repository<Exercise>(context), IExerciseRepository
{
    public async Task<Exercise?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Db.Exercises
            .Include(x => x.Muscles)
            .Include(x => x.Tags)
            .Include(x => x.Instructions)
            .Include(x => x.Media)
            .Include(x => x.Warnings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}