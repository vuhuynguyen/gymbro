using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.FoodModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Foods;

public sealed class FoodConfiguration : IEntityTypeConfiguration<Food>
{
    public void Configure(EntityTypeBuilder<Food> builder)
    {
        builder.ToTable("Foods");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Brand).HasMaxLength(200);
        builder.Property(x => x.Kind).IsRequired().HasConversion<int>();

        builder.Property(x => x.ServingLabel).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ServingSizeGrams).HasPrecision(8, 2);
        builder.Property(x => x.EnergyKcal).HasPrecision(8, 2);
        builder.Property(x => x.ProteinG).HasPrecision(8, 2);
        builder.Property(x => x.CarbsG).HasPrecision(8, 2);
        builder.Property(x => x.FatG).HasPrecision(8, 2);
        builder.Property(x => x.FiberG).HasPrecision(8, 2);

        // ISharedEntity: null TenantId = global catalog, non-null = a gym's custom food.
        builder.Property(x => x.TenantId).IsRequired(false);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.Kind });
    }
}
