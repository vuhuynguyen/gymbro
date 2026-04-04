using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.ExerciseModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Exercises;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DefaultName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DefaultDescription)
            .HasMaxLength(1000);

        // Enum stored as integer (Npgsql); do not use HasMaxLength on numeric enums.
        builder.Property(x => x.MuscleGroup)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.TenantId)
            .IsRequired(false);

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(x => new { x.TenantId, x.DefaultName });

        builder.HasMany(x => x.Instructions)
            .WithOne()
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Tags)
            .WithOne()
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Media)
            .WithOne()
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Warnings)
            .WithOne()
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Exercise.Instructions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(Exercise.Tags))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(Exercise.Media))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(Exercise.Warnings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
