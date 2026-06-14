using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Infrastructure.Persistence.Configurations;

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

        // A drop/rest-pause cluster's stage rows reference their lead set via ParentSetId. Deleting the lead
        // removes its stages (they are not standalone logical sets), so a cluster can never be left with
        // orphaned, parent-less-but-still-volume-bearing rows when the lead is deleted. Indexed for the
        // cascade and for stage lookups by parent.
        builder.HasOne<PerformedSet>()
            .WithMany()
            .HasForeignKey(x => x.ParentSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ParentSetId);
    }
}
