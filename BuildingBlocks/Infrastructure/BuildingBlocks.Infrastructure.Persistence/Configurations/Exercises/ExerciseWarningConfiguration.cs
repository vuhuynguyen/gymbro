using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.ExerciseModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Exercises;

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
