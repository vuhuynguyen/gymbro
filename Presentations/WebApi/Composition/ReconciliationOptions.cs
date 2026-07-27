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

    /// <summary>
    /// Master switch. Default <b>false</b>: this is a read-only drift <i>detector</i> (it never mutates), and
    /// the cross-store transaction already keeps the two stores consistent at write time — so on a
    /// single-instance / personal deployment it adds only a periodic pair of full-table scans that keep the
    /// database awake. Set <c>Reconciliation:Enabled=true</c> to opt back in (e.g. after suspected drift or on
    /// a multi-instance deploy).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>How often the consistency check runs (only when <see cref="Enabled"/>).</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Delay before the first run, so it does not contend with startup migrations/seeding.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(1);
}
