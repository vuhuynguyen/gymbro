using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.FoodModule.Entities;

/// <summary>
/// Food / supplement / beverage catalog item. Sibling of <c>Exercise</c>: an <see cref="ISharedEntity"/>
/// (global when <c>TenantId == null</c>, tenant-custom otherwise) + <see cref="ISoftDelete"/> aggregate.
/// Other modules reference foods via <c>FoodId</c> and the MediatR contracts in
/// <c>Modules.FoodModule.Application.Queries</c> only — never this entity (module-boundary rule).
///
/// <para>MVP nutrition is denormalized: a food carries one canonical serving and its headline macros
/// (energy/protein/carb/fat/fiber) for that serving. These are captured onto plan items and log items at
/// authoring/log time so renaming or retiring a food never rewrites history, and macro analytics are
/// available later with no extra user input. The normalized per-nutrient / multi-serving / translation /
/// provenance model in the proposal is an additive later phase — kept out here to match the current
/// <c>Exercise</c> catalog, which carries no slug/provenance columns.</para>
/// </summary>
public sealed class Food : AggregateRoot, ISharedEntity, ISoftDelete
{
    public string Name { get; private set; } = null!;
    public string? Brand { get; private set; }
    public FoodKind Kind { get; private set; }

    /// <summary>Human label for the canonical serving (e.g. "1 scoop", "100 g", "1 medium banana").</summary>
    public string ServingLabel { get; private set; } = null!;

    /// <summary>Mass of the canonical serving in grams, when known (lets per-100g be derived later). Nullable for count-based items (1 capsule).</summary>
    public decimal? ServingSizeGrams { get; private set; }

    // Headline macros PER canonical serving. All optional (a "no nutrition data" supplement is valid).
    public decimal? EnergyKcal { get; private set; }
    public decimal? ProteinG { get; private set; }
    public decimal? CarbsG { get; private set; }
    public decimal? FatG { get; private set; }
    public decimal? FiberG { get; private set; }

    private Food() { }

    /// <summary>Creates a global catalog food (TenantId null). Used by the platform-admin write path.</summary>
    public static Food CreateGlobal(
        string name,
        FoodKind kind,
        string servingLabel,
        decimal? servingSizeGrams,
        decimal? energyKcal,
        decimal? proteinG,
        decimal? carbsG,
        decimal? fatG,
        decimal? fiberG,
        string? brand)
        => Create(null, name, kind, servingLabel, servingSizeGrams, energyKcal, proteinG, carbsG, fatG, fiberG, brand);

    /// <summary>Creates a tenant-custom food (TenantId = owning gym). Used by the Owner write path.</summary>
    public static Food CreateForTenant(
        Guid tenantId,
        string name,
        FoodKind kind,
        string servingLabel,
        decimal? servingSizeGrams,
        decimal? energyKcal,
        decimal? proteinG,
        decimal? carbsG,
        decimal? fatG,
        decimal? fiberG,
        string? brand)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId is required for a custom food.");

        return Create(tenantId, name, kind, servingLabel, servingSizeGrams, energyKcal, proteinG, carbsG, fatG, fiberG, brand);
    }

    private static Food Create(
        Guid? tenantId,
        string name,
        FoodKind kind,
        string servingLabel,
        decimal? servingSizeGrams,
        decimal? energyKcal,
        decimal? proteinG,
        decimal? carbsG,
        decimal? fatG,
        decimal? fiberG,
        string? brand)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");
        if (string.IsNullOrWhiteSpace(servingLabel))
            throw new DomainException("Serving label is required.");
        GuardMacros(servingSizeGrams, energyKcal, proteinG, carbsG, fatG, fiberG);

        return new Food
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
            Kind = kind,
            ServingLabel = servingLabel.Trim(),
            ServingSizeGrams = servingSizeGrams,
            EnergyKcal = energyKcal,
            ProteinG = proteinG,
            CarbsG = carbsG,
            FatG = fatG,
            FiberG = fiberG,
            IsDeleted = false
        };
    }

    public void UpdateDetails(
        string name,
        FoodKind kind,
        string servingLabel,
        decimal? servingSizeGrams,
        decimal? energyKcal,
        decimal? proteinG,
        decimal? carbsG,
        decimal? fatG,
        decimal? fiberG,
        string? brand)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");
        if (string.IsNullOrWhiteSpace(servingLabel))
            throw new DomainException("Serving label is required.");
        GuardMacros(servingSizeGrams, energyKcal, proteinG, carbsG, fatG, fiberG);

        Name = name.Trim();
        Kind = kind;
        ServingLabel = servingLabel.Trim();
        ServingSizeGrams = servingSizeGrams;
        EnergyKcal = energyKcal;
        ProteinG = proteinG;
        CarbsG = carbsG;
        FatG = fatG;
        FiberG = fiberG;
        Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim();
    }

    private static void GuardMacros(
        decimal? servingSizeGrams, decimal? energyKcal,
        decimal? proteinG, decimal? carbsG, decimal? fatG, decimal? fiberG)
    {
        if (servingSizeGrams is <= 0) throw new DomainException("servingSizeGrams must be positive.");
        if (energyKcal is < 0) throw new DomainException("energyKcal is out of range.");
        if (proteinG is < 0) throw new DomainException("proteinG is out of range.");
        if (carbsG is < 0) throw new DomainException("carbsG is out of range.");
        if (fatG is < 0) throw new DomainException("fatG is out of range.");
        if (fiberG is < 0) throw new DomainException("fiberG is out of range.");
    }
}
