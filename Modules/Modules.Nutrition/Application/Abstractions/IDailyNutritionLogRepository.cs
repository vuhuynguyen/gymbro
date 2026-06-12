using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Abstractions;

/// <summary>Repository for daily nutrition logs. Mirrors <c>IWorkoutSessionRepository</c>.</summary>
public interface IDailyNutritionLogRepository
{
    Task AddAsync(DailyNutritionLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly tracks a NEW child item as <c>Added</c>. Required when adding an item to a day that was
    /// <em>loaded</em> (tracked <c>Unchanged</c>) rather than created in this scope: a child reached only
    /// through the parent's navigation collection has its app-assigned (<c>ValueGeneratedOnAdd</c>) Guid key
    /// mis-read by EF as an existing row, so SaveChanges emits a 0-row <c>UPDATE</c> instead of an
    /// <c>INSERT</c>. Mirrors how the WorkoutSession child repos register performed exercises/sets.
    /// </summary>
    void AddItem(LoggedItem item);

    /// <summary>
    /// The caller's own log for a date, tracked, with items — used to create-or-mutate a day. Bypasses the
    /// tenant filter (self-scoped, cross-gym) and re-applies soft-delete; only ever called with the caller's
    /// own id. Mirrors the session active-lookup bypass.
    /// </summary>
    Task<DailyNutritionLog?> GetOwnByDateAsync(Guid traineeId, DateOnly localDate, CancellationToken cancellationToken = default);

    /// <summary>The caller's own logs across every gym (audited bypass), for history reads + lazy day-close.</summary>
    IQueryable<DailyNutritionLog> QueryOwnAcrossGyms(Guid traineeId);

    /// <summary>
    /// Detaches a log (and its tracked items) from the change tracker. Used to drop a losing day-insert after
    /// the unique <c>(TraineeId, LocalDate)</c> race, so the write can be re-applied onto the winning day.
    /// </summary>
    void Detach(DailyNutritionLog log);

    /// <summary>Tenant-scoped query (global filter applies) — the coach's per-gym client-day reads.</summary>
    IQueryable<DailyNutritionLog> Query();
}
