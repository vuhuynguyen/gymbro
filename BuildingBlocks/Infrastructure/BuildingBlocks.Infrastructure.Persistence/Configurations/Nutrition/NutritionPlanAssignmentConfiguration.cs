using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.NutritionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Nutrition;

public sealed class NutritionPlanAssignmentConfiguration : IEntityTypeConfiguration<NutritionPlanAssignment>
{
    public void Configure(EntityTypeBuilder<NutritionPlanAssignment> builder)
    {
        builder.ToTable("NutritionPlanAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TraineeId).IsRequired();
        builder.Property(x => x.PlanId).IsRequired();
        builder.Property(x => x.PlanVersion).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate);
        builder.Property(x => x.VisibilityMode).HasConversion<int>().IsRequired();
        builder.Property(x => x.HideMacroTargets).HasDefaultValue(false);
        builder.Property(x => x.DisableTraineeEditing).HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb");

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(x => new { x.TenantId, x.TraineeId });
        builder.HasIndex(x => new { x.TenantId, x.PlanId });

        // At most one live assignment of a plan to a trainee; filtered on IsDeleted so a revoked assignment
        // doesn't block re-assigning later (mirrors PlanAssignment).
        builder.HasIndex(x => new { x.TenantId, x.TraineeId, x.PlanId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
