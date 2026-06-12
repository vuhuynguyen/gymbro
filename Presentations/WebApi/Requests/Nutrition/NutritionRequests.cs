using Modules.NutritionModule.Entities;

namespace WebApi.Requests.Nutrition;

// ── Plans (coach authoring) ───────────────────────────────────────────────

public sealed class CreateNutritionPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class NutritionPlanItemRequest
{
    public Guid FoodId { get; set; }
    public int Order { get; set; }
    public decimal Quantity { get; set; } = 1m;
}

public sealed class NutritionPlanMealRequest
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public TimeOnly? ScheduledTime { get; set; }
    public DayApplicability DayApplicability { get; set; } = DayApplicability.EveryDay;
    public List<NutritionPlanItemRequest> Items { get; set; } = [];
}

public sealed class ReplaceNutritionPlanStructureRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<NutritionPlanMealRequest> Meals { get; set; } = [];
}

// ── Assignments ───────────────────────────────────────────────────────────

public sealed class CreateNutritionAssignmentRequest
{
    public Guid TraineeId { get; set; }
    public Guid PlanId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public NutritionVisibilityMode VisibilityMode { get; set; } = NutritionVisibilityMode.Full;
    public bool HideMacroTargets { get; set; }
    public bool DisableTraineeEditing { get; set; }
}

/// <summary>Edits an existing nutrition assignment. Mirrors UpdatePlanAssignmentRequest.</summary>
public sealed class UpdateNutritionAssignmentRequest
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public NutritionVisibilityMode VisibilityMode { get; set; } = NutritionVisibilityMode.Full;
    public bool HideMacroTargets { get; set; }
    public bool DisableTraineeEditing { get; set; }
}

// ── Trainee daily-log writes (/api/me/nutrition) ──────────────────────────

public sealed class SetNutritionItemStatusRequest
{
    public DateOnly Date { get; set; }
    public Guid ItemId { get; set; }
    public LoggedItemStatus Status { get; set; }
    public string? Note { get; set; }
}

public sealed class SubstituteNutritionItemRequest
{
    public DateOnly Date { get; set; }
    public Guid ItemId { get; set; }
    public Guid FoodId { get; set; }
    public decimal? Quantity { get; set; }
    public string? Note { get; set; }
}

/// <summary>Daily check-in metric (weight/sleep/…); <c>LocalDate</c> defaults to UTC today when omitted.</summary>
public sealed class LogMetricEntryRequest
{
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Unit { get; set; }
    public DateOnly? LocalDate { get; set; }
}

public sealed class AddAdhocNutritionItemRequest
{
    public DateOnly Date { get; set; }

    /// <summary>A catalog food id, OR null with the Custom* fields set for an inline custom food.</summary>
    public Guid? FoodId { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public string? MealName { get; set; }
    public string? Note { get; set; }

    // ── Inline custom food (no catalog entry) ──
    public string? CustomName { get; set; }
    public string? CustomKind { get; set; }
    public string? ServingLabel { get; set; }
    public decimal? EnergyKcal { get; set; }
    public decimal? ProteinG { get; set; }
    public decimal? CarbsG { get; set; }
    public decimal? FatG { get; set; }
    public decimal? FiberG { get; set; }
}
