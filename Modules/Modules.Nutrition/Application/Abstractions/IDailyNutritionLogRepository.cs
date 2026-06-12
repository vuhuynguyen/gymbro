using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Abstractions;

/// <summary>Repository for daily nutrition logs. Mirrors <c>IWorkoutSessionRepository</c>.</summary>
public interface IDailyNutritionLogRepository
{
    Task AddAsync(DailyNutritionLog log, CancellationToken cancellationToken = default);

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
