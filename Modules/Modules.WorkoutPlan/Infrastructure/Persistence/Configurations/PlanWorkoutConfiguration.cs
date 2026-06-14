using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Infrastructure.Persistence.Configurations;

public sealed class PlanWorkoutConfiguration : IEntityTypeConfiguration<PlanWorkout>
{
    public void Configure(EntityTypeBuilder<PlanWorkout> builder)
    {
        builder.ToTable("PlanWorkouts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.Order)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.HasMany(x => x.Exercises)
            .WithOne()
            .HasForeignKey(x => x.PlanWorkoutId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(PlanWorkout.Exercises))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
