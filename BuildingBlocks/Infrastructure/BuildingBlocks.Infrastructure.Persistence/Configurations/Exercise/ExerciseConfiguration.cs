using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ExerciseRoot = Modules.Exercise.Entities.Exercise;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Exercise;

public class ExerciseConfiguration : IEntityTypeConfiguration<ExerciseRoot>
{
    public void Configure(EntityTypeBuilder<ExerciseRoot> builder)
    {
        builder.ToTable("Exercises");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DefaultName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DefaultDescription)
            .HasMaxLength(1000);

        builder.Property(x => x.MuscleGroup)
            .IsRequired()
            .HasMaxLength(100);

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
            .FindNavigation(nameof(ExerciseRoot.Instructions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(ExerciseRoot.Tags))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(ExerciseRoot.Media))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(ExerciseRoot.Warnings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
