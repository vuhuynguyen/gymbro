using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.ExerciseModule.Entities;
using Modules.WorkoutPlanModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.WorkoutPlans;

public sealed class PlanWorkoutExerciseConfiguration : IEntityTypeConfiguration<PlanWorkoutExercise>
{
    public void Configure(EntityTypeBuilder<PlanWorkoutExercise> builder)
    {
        builder.ToTable("PlanWorkoutExercises");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Order).IsRequired();

        builder.HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.PrescribedSets)
            .WithOne()
            .HasForeignKey(x => x.PlanWorkoutExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(PlanWorkoutExercise.PrescribedSets))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.PlanWorkoutId, x.Order });
    }
}
