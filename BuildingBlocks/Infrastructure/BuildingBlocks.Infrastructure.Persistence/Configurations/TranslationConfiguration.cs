using BuildingBlocks.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations;

public class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.ToTable("Translations");

        // ========================
        // Primary Key
        // ========================
        builder.HasKey(x => x.Id);

        // ========================
        // Properties
        // ========================
        builder.Property(x => x.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.Language)
            .IsRequired()
            .HasMaxLength(5)
            .IsUnicode(false); // "en", "vi"

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(2000); // allow long content (instruction, description)

        // ========================
        // Indexes (VERY IMPORTANT)
        // ========================

        // 🔥 Core uniqueness constraint
        builder.HasIndex(x => new
        {
            x.EntityType,
            x.EntityId,
            x.Language,
            x.Key
        }).IsUnique();

        // 🔍 Fast lookup by entity
        builder.HasIndex(x => new
        {
            x.EntityType,
            x.EntityId
        });

        // 🔍 Optional: query by language
        builder.HasIndex(x => x.Language);

        // ========================
        // Optional: soft delete (if needed later)
        // ========================
        // builder.Property(x => x.IsDeleted).HasDefaultValue(false);
    }
}