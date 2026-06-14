using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Infrastructure.Persistence.Configurations;

public sealed class MetricEntryConfiguration : IEntityTypeConfiguration<MetricEntry>
{
    public void Configure(EntityTypeBuilder<MetricEntry> builder)
    {
        builder.ToTable("MetricEntries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TraineeId).IsRequired();
        builder.Property(x => x.Type).IsRequired().HasMaxLength(MetricEntry.TypeMaxLength);
        builder.Property(x => x.Value).IsRequired().HasPrecision(8, 2);
        builder.Property(x => x.Unit).HasMaxLength(MetricEntry.UnitMaxLength);
        builder.Property(x => x.LocalDate).IsRequired();
        builder.Property(x => x.LoggedAtUtc).IsRequired();

        // The series is personal and cross-gym (no TenantId — see the entity doc); reads are always
        // "this trainee's entries on this date", newest first.
        builder.HasIndex(x => new { x.TraineeId, x.LocalDate })
            .HasDatabaseName("IX_MetricEntries_TraineeId_LocalDate");
    }
}
