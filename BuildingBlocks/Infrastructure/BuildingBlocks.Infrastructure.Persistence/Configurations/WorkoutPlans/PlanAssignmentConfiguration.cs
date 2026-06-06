using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WorkoutPlanModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.WorkoutPlans;

public sealed class PlanAssignmentConfiguration : IEntityTypeConfiguration<PlanAssignment>
{
    public void Configure(EntityTypeBuilder<PlanAssignment> builder)
    {
        builder.ToTable("PlanAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TraineeId).IsRequired();
        builder.Property(x => x.PlanId).IsRequired();
        builder.Property(x => x.PlanVersion).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.FrequencyDaysPerWeek).IsRequired();
        builder.Property(x => x.VisibilityMode).HasConversion<int>().IsRequired();
        builder.Property(x => x.HideExercises).HasDefaultValue(false);
        builder.Property(x => x.HideSetsReps).HasDefaultValue(false);
        builder.Property(x => x.HideFutureWorkouts).HasDefaultValue(false);
        builder.Property(x => x.DisableTraineeEditing).HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb");

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(x => new { x.TenantId, x.TraineeId });
        builder.HasIndex(x => new { x.TenantId, x.PlanId });

        // At most one live assignment of a given plan to a given trainee; filtered on IsDeleted so a
        // soft-deleted (revoked) assignment does not block re-assigning the same plan later.
        builder.HasIndex(x => new { x.TenantId, x.TraineeId, x.PlanId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
