using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.FoodModule.Entities;
using Modules.NutritionModule.Entities;

namespace WebApi.Persistence.Configurations;

public sealed class PlanMealItemConfiguration : IEntityTypeConfiguration<PlanMealItem>
{
    public void Configure(EntityTypeBuilder<PlanMealItem> builder)
    {
        builder.ToTable("PlanMealItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PlanMealId).IsRequired();
        builder.Property(x => x.FoodId).IsRequired();
        builder.Property(x => x.Order).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(8, 2).IsRequired();

        builder.Property(x => x.FoodNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ServingLabelSnapshot).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EnergyKcal).HasPrecision(8, 2);
        builder.Property(x => x.ProteinG).HasPrecision(8, 2);
        builder.Property(x => x.CarbsG).HasPrecision(8, 2);
        builder.Property(x => x.FatG).HasPrecision(8, 2);
        builder.Property(x => x.FiberG).HasPrecision(8, 2);

        // Cross-module FK to the catalog (Restrict), mirroring PerformedExercise → Exercise. Food
        // soft-deletes, so Restrict never blocks (the delete is an UPDATE).
        builder.HasOne<Food>()
            .WithMany()
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PlanMealId, x.Order });
    }
}
