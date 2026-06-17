using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// Carrier for creating a logged item (planned seed or ad-hoc). Macro fields are the snapshot captured at
/// seed/log time so a later food edit never rewrites a closed day. <see cref="FoodId"/> is null for an
/// inline custom item (one the trainee typed themselves — no catalog entry); its snapshot fields carry the
/// food data instead. <see cref="Kind"/> is the food kind snapshot ("Food"/"Supplement"/"Beverage") so the
/// checklist can mark supplements/beverages — a string (not the Food module's enum) to keep the module
/// boundary clean, exactly like <see cref="FoodNameSnapshot"/>.
/// </summary>
public sealed record LoggedItemData(
    Guid? PlanMealItemId,
    string MealName,
    TimeOnly? ScheduledTime,
    int Order,
    Guid? FoodId,
    string Kind,
    string FoodNameSnapshot,
    string ServingLabelSnapshot,
    decimal Quantity,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG,
    Guid? ClientItemId = null);

/// <summary>
/// The unit of nutrition logging: a planned-or-ad-hoc food entry with a planned-vs-actual status and a
/// durable nutrition snapshot. Sibling of <c>PerformedExercise</c>.
/// </summary>
public sealed class LoggedItem : BaseEntity, ITenantEntity
{
    public Guid DailyNutritionLogId { get; private set; }
    /// <summary>The plan item this fulfils. Null ⇒ ad-hoc / off-plan.</summary>
    public Guid? PlanMealItemId { get; private set; }
    /// <summary>Client-generated id for idempotent (offline-tolerant) ad-hoc creates: a replay of the same
    /// create is a no-op success. Unique per day (filtered). Null for planned/snapshot-seeded items.</summary>
    public Guid? ClientItemId { get; private set; }
    public string MealName { get; private set; } = null!;
    public TimeOnly? ScheduledTime { get; private set; }
    public int Order { get; private set; }

    /// <summary>The catalog food. Null ⇒ an inline custom item (trainee-typed, no catalog entry).</summary>
    public Guid? FoodId { get; private set; }
    /// <summary>Food kind snapshot ("Food"/"Supplement"/"Beverage") so the checklist can tag supplements
    /// & drinks. A string (not the Food enum) to keep the module boundary clean.</summary>
    public string Kind { get; private set; } = null!;
    public Guid? SubstitutedFromFoodId { get; private set; }
    public string FoodNameSnapshot { get; private set; } = null!;
    public string ServingLabelSnapshot { get; private set; } = null!;
    public decimal Quantity { get; private set; }

    public decimal? EnergyKcal { get; private set; }
    public decimal? ProteinG { get; private set; }
    public decimal? CarbsG { get; private set; }
    public decimal? FatG { get; private set; }
    public decimal? FiberG { get; private set; }

    public LoggedItemStatus Status { get; private set; }
    public DateTimeOffset? LoggedAtUtc { get; private set; }
    public string? Note { get; private set; }

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private LoggedItem() { }

    /// <summary>Seeds a planned item (Status = Planned) from a plan snapshot.</summary>
    public static LoggedItem Planned(Guid logId, Guid tenantId, LoggedItemData d)
        => Create(logId, tenantId, d, LoggedItemStatus.Planned, loggedAt: null);

    /// <summary>Logs an ad-hoc (off-plan) item, already Completed.</summary>
    public static LoggedItem Adhoc(Guid logId, Guid tenantId, LoggedItemData d, string? note)
    {
        var item = Create(logId, tenantId, d with { PlanMealItemId = null }, LoggedItemStatus.Completed, DateTimeOffset.UtcNow);
        item.Note = note;
        return item;
    }

    private static LoggedItem Create(Guid logId, Guid tenantId, LoggedItemData d, LoggedItemStatus status, DateTimeOffset? loggedAt)
    {
        if (logId == Guid.Empty) throw new DomainException("DailyNutritionLogId is required.");
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");
        // FoodId may be null (inline custom item); a custom item must still carry a name (validated upstream).
        if (d.Quantity <= 0) throw new DomainException("Quantity must be positive.");

        return new LoggedItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DailyNutritionLogId = logId,
            PlanMealItemId = d.PlanMealItemId,
            ClientItemId = d.ClientItemId,
            MealName = string.IsNullOrWhiteSpace(d.MealName) ? "Meal" : d.MealName.Trim(),
            ScheduledTime = d.ScheduledTime,
            Order = d.Order,
            FoodId = d.FoodId == Guid.Empty ? null : d.FoodId,
            Kind = string.IsNullOrWhiteSpace(d.Kind) ? "Food" : d.Kind.Trim(),
            FoodNameSnapshot = string.IsNullOrWhiteSpace(d.FoodNameSnapshot) ? "(food)" : d.FoodNameSnapshot.Trim(),
            ServingLabelSnapshot = string.IsNullOrWhiteSpace(d.ServingLabelSnapshot) ? "1 serving" : d.ServingLabelSnapshot.Trim(),
            Quantity = d.Quantity,
            EnergyKcal = d.EnergyKcal,
            ProteinG = d.ProteinG,
            CarbsG = d.CarbsG,
            FatG = d.FatG,
            FiberG = d.FiberG,
            Status = status,
            LoggedAtUtc = loggedAt
        };
    }

    public void Complete(string? note)
    {
        Status = LoggedItemStatus.Completed;
        LoggedAtUtc = DateTimeOffset.UtcNow;
        if (note != null) Note = note;
    }

    public void Skip(string? note)
    {
        Status = LoggedItemStatus.Skipped;
        LoggedAtUtc = DateTimeOffset.UtcNow;
        if (note != null) Note = note;
    }

    /// <summary>Swaps the food, preserving provenance, and marks the item Substituted (counts as adherent).</summary>
    public void Substitute(Guid newFoodId, string kind, string foodName, string servingLabel, decimal quantity,
        decimal? energyKcal, decimal? proteinG, decimal? carbsG, decimal? fatG, decimal? fiberG, string? note)
    {
        if (newFoodId == Guid.Empty) throw new DomainException("FoodId is required.");
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");

        SubstitutedFromFoodId = FoodId;
        FoodId = newFoodId;
        Kind = string.IsNullOrWhiteSpace(kind) ? Kind : kind.Trim();
        FoodNameSnapshot = string.IsNullOrWhiteSpace(foodName) ? FoodNameSnapshot : foodName.Trim();
        ServingLabelSnapshot = string.IsNullOrWhiteSpace(servingLabel) ? ServingLabelSnapshot : servingLabel.Trim();
        Quantity = quantity;
        EnergyKcal = energyKcal;
        ProteinG = proteinG;
        CarbsG = carbsG;
        FatG = fatG;
        FiberG = fiberG;
        Status = LoggedItemStatus.Substituted;
        LoggedAtUtc = DateTimeOffset.UtcNow;
        if (note != null) Note = note;
    }

    /// <summary>Reverts a planned item back to Planned (un-tick) and clears its logged timestamp.</summary>
    public void ResetToPlanned()
    {
        if (PlanMealItemId == null) throw new DomainException("Only planned items can be reset to Planned.");

        Status = LoggedItemStatus.Planned;
        LoggedAtUtc = null;
    }

    /// <summary>Marks a still-Planned item as Missed at day close (no-show). Other statuses are untouched.</summary>
    public void MarkMissedIfPlanned()
    {
        if (Status == LoggedItemStatus.Planned)
            Status = LoggedItemStatus.Missed;
    }

    /// <summary>A planned item counts toward adherence (not ad-hoc).</summary>
    public bool IsPlanned => PlanMealItemId != null;

    /// <summary>Completed or Substituted planned items are adherent.</summary>
    public bool IsAdherent => IsPlanned && Status is LoggedItemStatus.Completed or LoggedItemStatus.Substituted;
}
