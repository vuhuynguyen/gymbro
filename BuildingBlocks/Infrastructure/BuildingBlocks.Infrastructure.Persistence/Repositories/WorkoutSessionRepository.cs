using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class WorkoutSessionRepository(AppDbContext context) : IWorkoutSessionRepository
{
    public async Task AddAsync(WorkoutSession session, CancellationToken ct = default)
        => await context.Set<WorkoutSession>().AddAsync(session, ct);

    public async Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<WorkoutSession>().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<WorkoutSession?> GetWithExercisesAndSetsAsync(Guid id, CancellationToken ct = default)
        => await context.Set<WorkoutSession>()
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<WorkoutSession?> GetActiveForTraineeAsync(Guid traineeId, CancellationToken ct = default)
        => await context.Set<WorkoutSession>()
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(s => s.TraineeId == traineeId && s.Status == SessionStatus.InProgress, ct);

    public IQueryable<WorkoutSession> Query()
        => context.Set<WorkoutSession>().AsQueryable();
}
