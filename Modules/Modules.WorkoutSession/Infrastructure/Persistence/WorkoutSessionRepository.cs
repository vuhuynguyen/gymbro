using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Infrastructure.Persistence;

public sealed class WorkoutSessionRepository(DbContext context) : IWorkoutSessionRepository
{
    public async Task AddAsync(WorkoutSession session, CancellationToken ct = default)
        => await context.Set<WorkoutSession>().AddAsync(session, ct);

    public async Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<WorkoutSession>().FirstOrDefaultAsync(s => s.Id == id, ct);

    // Read-only paths (GetSessionById / GetActiveSession / StartSession's existence check) — AsNoTracking
    // skips change-tracking snapshots for the session+exercises+sets graph. Never used to load-then-mutate.
    public async Task<WorkoutSession?> GetWithExercisesAndSetsAsync(Guid id, CancellationToken ct = default)
        => await context.Set<WorkoutSession>()
            .AsNoTracking()
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    // User-scoped active-session lookup: bypasses the tenant filter so "your one active session" is found
    // wherever it lives, enforcing one in-progress session per USER regardless of gym. Re-applies the
    // soft-delete predicate that IgnoreQueryFilters() also drops. Only ever called with the caller's own id.
    public async Task<WorkoutSession?> GetActiveForTraineeAsync(Guid traineeId, CancellationToken ct = default)
        => await context.Set<WorkoutSession>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(
                s => s.TraineeId == traineeId && s.Status == SessionStatus.InProgress && !s.IsDeleted, ct);

    public IQueryable<WorkoutSession> Query()
        => context.Set<WorkoutSession>().AsQueryable();

    // See IWorkoutSessionRepository.QueryOwnAcrossGyms — deliberate, audited tenant-filter bypass that
    // returns only the caller's own sessions across all gyms, with soft-delete re-applied.
    public IQueryable<WorkoutSession> QueryOwnAcrossGyms(Guid traineeId)
        => context.Set<WorkoutSession>()
            .IgnoreQueryFilters()
            .Where(s => s.TraineeId == traineeId && !s.IsDeleted);
}
