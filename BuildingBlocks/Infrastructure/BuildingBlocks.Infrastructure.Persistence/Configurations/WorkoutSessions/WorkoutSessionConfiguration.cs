using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WorkoutSessionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.WorkoutSessions;

public sealed class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> builder)
    {
        builder.ToTable("WorkoutSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.TraineeId).IsRequired();
        builder.Property(x => x.Source).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.BodyweightKg).HasPrecision(5, 1);
        builder.Property(x => x.WorkoutNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.ClientTimezone).HasMaxLength(60);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.Navigation(x => x.Exercises)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.TraineeId, x.StartedAt });
        builder.HasIndex(x => new { x.TraineeId, x.Status });
    }
}
