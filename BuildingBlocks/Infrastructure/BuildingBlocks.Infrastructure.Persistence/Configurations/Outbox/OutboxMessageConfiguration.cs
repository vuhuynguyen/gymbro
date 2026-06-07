using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OccurredOnUtc).IsRequired();
        builder.Property(x => x.Type).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Content).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.Error).HasMaxLength(2000);

        // The processor scans for unprocessed rows oldest-first (WHERE ProcessedOnUtc IS NULL ORDER BY
        // OccurredOnUtc); this composite index serves that poll and the retention purge (ProcessedOnUtc prefix).
        builder.HasIndex(x => new { x.ProcessedOnUtc, x.OccurredOnUtc });
    }
}
