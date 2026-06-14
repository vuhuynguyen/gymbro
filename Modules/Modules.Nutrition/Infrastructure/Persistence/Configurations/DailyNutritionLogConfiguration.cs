using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Infrastructure.Persistence.Configurations;

public sealed class DailyNutritionLogConfiguration : IEntityTypeConfiguration<DailyNutritionLog>
{
    public void Configure(EntityTypeBuilder<DailyNutritionLog> builder)
    {
        builder.ToTable("DailyNutritionLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.TraineeId).IsRequired();
        builder.Property(x => x.LocalDate).IsRequired();
        builder.Property(x => x.ClientTimezone).HasMaxLength(60);
        builder.Property(x => x.Source).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.AdherencePct).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb");

        builder.Navigation(x => x.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // One nutrition day per USER per local date, across every gym (a person has a single nutrition
        // timeline). The TenantId stamps which gym the day was opened under. Mirrors the session
        // one-active-per-user invariant, keyed by date.
        builder.HasIndex(x => new { x.TraineeId, x.LocalDate })
            .IsUnique()
            .HasDatabaseName("IX_DailyNutritionLogs_TraineeId_LocalDate");

        // Coach per-gym client-day read.
        builder.HasIndex(x => new { x.TenantId, x.TraineeId, x.LocalDate });
    }
}
