using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WorkoutPlanModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.WorkoutPlans;

public sealed class PlanWorkoutExerciseSetConfiguration : IEntityTypeConfiguration<PlanWorkoutExerciseSet>
{
    public void Configure(EntityTypeBuilder<PlanWorkoutExerciseSet> builder)
    {
        builder.ToTable("PlanWorkoutExerciseSets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Order).IsRequired();
        builder.Property(x => x.SetType).IsRequired();
        builder.Property(x => x.RestSeconds).IsRequired();
        builder.Property(x => x.TargetWeightKg).HasPrecision(6, 2);

        builder.HasIndex(x => new { x.PlanWorkoutExerciseId, x.Order });
    }
}
