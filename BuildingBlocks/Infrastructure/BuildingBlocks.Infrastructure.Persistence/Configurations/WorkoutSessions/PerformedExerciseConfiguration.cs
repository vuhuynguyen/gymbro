using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.ExerciseModule.Entities;
using Modules.WorkoutSessionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.WorkoutSessions;

public sealed class PerformedExerciseConfiguration : IEntityTypeConfiguration<PerformedExercise>
{
    public void Configure(EntityTypeBuilder<PerformedExercise> builder)
    {
        builder.ToTable("PerformedExercises");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SessionId).IsRequired();
        builder.Property(x => x.ExerciseId).IsRequired();
        builder.Property(x => x.ExerciseName).HasMaxLength(200);
        builder.Property(x => x.Order).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkoutSession>()
            .WithMany(s => s.Exercises)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Sets)
            .WithOne()
            .HasForeignKey(x => x.PerformedExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(PerformedExercise.Sets))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.SessionId, x.Order });
    }
}
