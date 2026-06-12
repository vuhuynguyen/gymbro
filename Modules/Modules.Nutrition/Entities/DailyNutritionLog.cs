using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// One nutrition day per (trainee, local date). Created lazily on first touch (snapshot-on-touch): it
/// snapshots the applicable planned meals and seeds one <see cref="LoggedItem"/> per planned item, then every
/// interaction is a status transition on an item or an ad-hoc add. Sibling of <c>WorkoutSession</c> — same
/// snapshot-and-denormalize pattern, keyed by date instead of an in-progress flag.
/// </summary>
public sealed class DailyNutritionLog : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TraineeId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public string? ClientTimezone { get; private set; }
    public Guid? NutritionPlanAssignmentId { get; private set; }
    public NutritionSource Source { get; private set; }
    public string? SnapshotJson { get; private set; }
    public DailyLogStatus Status { get; private set; }

    /// <summary>Finalized adherence % (completed/substituted planned items ÷ planned items), set on close.</summary>
    public int AdherencePct { get; private set; }

    private readonly List<LoggedItem> _items = new();
    public IReadOnlyCollection<LoggedItem> Items => _items;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private DailyNutritionLog() { }

    public static DailyNutritionLog Open(
        Guid traineeId,
        Guid tenantId,
        DateOnly localDate,
        string? clientTimezone,
        NutritionSource source,
        Guid? assignmentId,
        string? snapshotJson)
    {
        if (traineeId == Guid.Empty) throw new DomainException("TraineeId is required.");
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");

        return new DailyNutritionLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TraineeId = traineeId,
            LocalDate = localDate,
            ClientTimezone = clientTimezone,
            Source = source,
            NutritionPlanAssignmentId = assignmentId,
            SnapshotJson = snapshotJson,
            Status = DailyLogStatus.Open,
            AdherencePct = 0,
            IsDeleted = false
        };
    }

    /// <summary>
    /// Opens a plan-less, self-logged day for a self-training user (no active assignment governs the date).
    /// The day is attributed to the user's own gym (<paramref name="tenantId"/>), carries no assignment and no
    /// snapshot, and is purely ad-hoc — items added later are off-plan, so adherence is 100% by convention.
    /// </summary>
    public static DailyNutritionLog OpenSelfLogged(
        Guid traineeId,
        Guid tenantId,
        DateOnly localDate,
        string? clientTimezone)
    {
        if (traineeId == Guid.Empty) throw new DomainException("TraineeId is required.");
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");

        return new DailyNutritionLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TraineeId = traineeId,
            LocalDate = localDate,
            ClientTimezone = clientTimezone,
            Source = NutritionSource.Adhoc,
            NutritionPlanAssignmentId = null,
            SnapshotJson = null,
            Status = DailyLogStatus.Open,
            AdherencePct = 0,
            IsDeleted = false
        };
    }

    public bool IsOpen => Status == DailyLogStatus.Open;

    public LoggedItem? FindItem(Guid itemId) => _items.FirstOrDefault(i => i.Id == itemId);

    public void SeedPlannedItems(IEnumerable<LoggedItemData> planned)
    {
        ArgumentNullException.ThrowIfNull(planned);
        var tenantId = TenantId ?? throw new InvalidOperationException("TenantId is not set.");
        foreach (var d in planned.OrderBy(x => x.Order))
            _items.Add(LoggedItem.Planned(Id, tenantId, d));
    }

    public LoggedItem AddAdhocItem(LoggedItemData data, string? note)
    {
        var tenantId = TenantId ?? throw new InvalidOperationException("TenantId is not set.");
        var nextOrder = (_items.Count == 0 ? 0 : _items.Max(i => i.Order)) + 1;
        var item = LoggedItem.Adhoc(Id, tenantId, data with { Order = nextOrder }, note);
        _items.Add(item);
        return item;
    }

    /// <summary>Removes an ad-hoc item. Planned items belong to the prescribed plan — they're skipped, never
    /// deleted, so the day's adherence denominator stays honest.</summary>
    public void RemoveAdhocItem(LoggedItem item)
    {
        if (item.IsPlanned)
            throw new DomainException("Planned items can't be removed — skip them instead.");
        _items.Remove(item);
    }

    /// <summary>
    /// Finalizes the day: still-Planned items become Missed, adherence is computed and stored, status flips
    /// to Closed, and <see cref="DailyLogClosedEvent"/> is raised. Idempotent (a second close is a no-op).
    /// </summary>
    public void Close()
    {
        if (Status == DailyLogStatus.Closed)
            return;

        foreach (var item in _items)
            item.MarkMissedIfPlanned();

        AdherencePct = ComputeAdherencePct(_items);
        Status = DailyLogStatus.Closed;

        var missedCount = _items.Count(i => i.Status == LoggedItemStatus.Missed);
        RaiseDomainEvent(new DailyLogClosedEvent(
            Id, TraineeId, TenantId!.Value, LocalDate, AdherencePct, missedCount, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Basic adherence: completed-or-substituted planned items ÷ planned items, as a 0–100 percentage.
    /// A day with no planned items (pure ad-hoc) is 100% by convention. The single formula — used live
    /// (open days), at close, and by the read-side list projections (via the count overload).
    /// </summary>
    public static int ComputeAdherencePct(int plannedCount, int adherentCount)
        => plannedCount == 0
            ? 100
            : (int)Math.Round(100.0 * adherentCount / plannedCount, MidpointRounding.AwayFromZero);

    public static int ComputeAdherencePct(IReadOnlyCollection<LoggedItem> items)
        => ComputeAdherencePct(items.Count(i => i.IsPlanned), items.Count(i => i.IsAdherent));
}
