using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Infrastructure.Persistence.Configurations;

public sealed class NutritionPlanConfiguration : IEntityTypeConfiguration<NutritionPlan>
{
    public void Configure(EntityTypeBuilder<NutritionPlan> builder)
    {
        builder.ToTable("NutritionPlans");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.TemplateId).IsRequired();
        builder.Property(x => x.Version).IsRequired().HasDefaultValue(1);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.IsArchived).HasDefaultValue(false);
        builder.Property(x => x.IsDraft).HasDefaultValue(false);

        builder.HasIndex(x => new { x.TenantId, x.Name });
        // Uniqueness covers PUBLISHED versions only: the single draft head is replaced in place (a new row at the
        // same version while the old is deleted in the same unit of work), so drafts must be exempt or that swap
        // would trip the index.
        builder.HasIndex(x => new { x.TemplateId, x.Version })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"IsDraft\" = false");

        builder.HasMany(x => x.Meals)
            .WithOne()
            .HasForeignKey(x => x.NutritionPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(NutritionPlan.Meals))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
