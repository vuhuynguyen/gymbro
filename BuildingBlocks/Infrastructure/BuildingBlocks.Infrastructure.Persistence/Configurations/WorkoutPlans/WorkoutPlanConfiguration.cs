using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WorkoutPlanModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.WorkoutPlans;

public sealed class WorkoutPlanConfiguration : IEntityTypeConfiguration<WorkoutPlan>
{
    public void Configure(EntityTypeBuilder<WorkoutPlan> builder)
    {
        builder.ToTable("WorkoutPlans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.TemplateId)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.DurationWeeks);
        builder.Property(x => x.WorkoutsPerWeek);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TemplateId, x.Version })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasMany(x => x.Workouts)
            .WithOne()
            .HasForeignKey(x => x.WorkoutPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(WorkoutPlan.Workouts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
