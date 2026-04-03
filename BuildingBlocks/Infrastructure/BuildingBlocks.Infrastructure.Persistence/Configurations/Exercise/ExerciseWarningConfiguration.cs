using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Exercise.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Exercise;

public class ExerciseWarningConfiguration : IEntityTypeConfiguration<ExerciseWarning>
{
    public void Configure(EntityTypeBuilder<ExerciseWarning> builder)
    {
        builder.ToTable("ExerciseWarnings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(500);
    }
}
