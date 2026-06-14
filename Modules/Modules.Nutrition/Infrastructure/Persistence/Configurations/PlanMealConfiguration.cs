using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Infrastructure.Persistence.Configurations;

public sealed class PlanMealConfiguration : IEntityTypeConfiguration<PlanMeal>
{
    public void Configure(EntityTypeBuilder<PlanMeal> builder)
    {
        builder.ToTable("PlanMeals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.NutritionPlanId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Order).IsRequired();
        builder.Property(x => x.ScheduledTime);
        builder.Property(x => x.DayApplicability).IsRequired().HasConversion<int>();

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PlanMealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(PlanMeal.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.NutritionPlanId, x.Order });
    }
}
