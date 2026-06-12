using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Abstractions;

/// <summary>
/// Shared get-or-create for the caller's own nutrition day on the self surface, used by every write that must
/// land on a day (today read's seed path + ad-hoc logging). Centralizes the snapshot-on-touch create+seed
/// logic so the read handler and the ad-hoc write handler can't drift.
/// </summary>
public interface INutritionDayProvisioner
{
    /// <summary>
    /// Returns the caller's tracked day for <paramref name="date"/>, creating it if needed:
    /// <list type="bullet">
    /// <item>an existing day is returned as-is (tracked, with items);</item>
    /// <item>else if an active assignment governs the date, a day is created + seeded from its snapshot;</item>
    /// <item>else a plan-less, ad-hoc <c>OpenSelfLogged</c> day is created, stamped with the active gym
    /// (<c>ITenantContext.TenantId</c> — this write surface is tenant-scoped). If no tenant is in context,
    /// returns <c>null</c>.</item>
    /// </list>
    /// The provisioner does NOT call SaveChanges — the calling handler owns the unit of work and saves after
    /// mutating the returned day. (The only persistence concern, the unique-day race, is handled by the
    /// caller's own save; on a duplicate-key conflict the caller re-reads the day.)
    /// </summary>
    Task<DailyNutritionLog?> GetOrCreateForWriteAsync(
        Guid userId, DateOnly date, string? timezone, CancellationToken ct);

    /// <summary>
    /// The assignment-seeded create path only: returns the existing tracked day, else creates+seeds from the
    /// active assignment, else <c>null</c> (no assignment — caller decides the no-plan behavior). Used by the
    /// read handler, which must NOT create a self-logged row just from reading.
    /// </summary>
    Task<DailyNutritionLog?> GetOrCreateFromAssignmentAsync(
        Guid userId, DateOnly date, string? timezone, CancellationToken ct);
}
