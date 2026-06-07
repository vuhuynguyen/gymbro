using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WorkoutSessionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.WorkoutSessions;

public sealed class PerformedSetConfiguration : IEntityTypeConfiguration<PerformedSet>
{
    public void Configure(EntityTypeBuilder<PerformedSet> builder)
    {
        builder.ToTable("PerformedSets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PerformedExerciseId).IsRequired();
        builder.Property(x => x.SetNumber).IsRequired();
        builder.Property(x => x.SetType).IsRequired();
        builder.Property(x => x.IsCompleted).IsRequired();
        builder.Property(x => x.LoggedAt).IsRequired();
        builder.Property(x => x.WeightKg).HasPrecision(6, 2);
        builder.Property(x => x.EstimatedOneRepMaxKg).HasPrecision(6, 1);

        builder.HasIndex(x => new { x.PerformedExerciseId, x.SetNumber });
        builder.HasIndex(x => x.LoggedAt);
    }
}
