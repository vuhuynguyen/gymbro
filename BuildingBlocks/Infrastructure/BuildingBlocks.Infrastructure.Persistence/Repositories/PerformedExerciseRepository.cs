using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class PerformedExerciseRepository(AppDbContext context) : IPerformedExerciseRepository
{
    public async Task AddAsync(PerformedExercise exercise, CancellationToken ct = default)
        => await context.Set<PerformedExercise>().AddAsync(exercise, ct);

    public async Task<PerformedExercise?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<PerformedExercise>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<PerformedExercise?> GetByIdWithSetsAsync(Guid id, CancellationToken ct = default)
        => await context.Set<PerformedExercise>()
            .Include(e => e.Sets)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public IQueryable<PerformedExercise> Query()
        => context.Set<PerformedExercise>().AsQueryable();
}
