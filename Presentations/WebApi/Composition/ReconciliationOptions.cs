namespace WebApi.Composition;

/// <summary>
/// Tunables for the cross-store reconciliation check, bound from the "Reconciliation" config section.
/// The Identity store (<c>AppUser</c>) and the domain store (<c>User</c>) are linked only by the convention
/// <c>AppUser.DomainUserId == User.Id</c> with no cross-store FK; the cross-store transaction keeps them
/// consistent at write time, and this check is the durable safety net that surfaces any drift that slips
/// past it.
/// </summary>
public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    /// <summary>Master switch; set false to disable the periodic check entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the consistency check runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Delay before the first run, so it does not contend with startup migrations/seeding.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(1);
}
