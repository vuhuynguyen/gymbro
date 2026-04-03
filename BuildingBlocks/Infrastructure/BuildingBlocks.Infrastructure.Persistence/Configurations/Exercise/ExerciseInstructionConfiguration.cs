using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Exercise.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Exercise;

public class ExerciseInstructionConfiguration : IEntityTypeConfiguration<ExerciseInstruction>
{
    public void Configure(EntityTypeBuilder<ExerciseInstruction> builder)
    {
        builder.ToTable("ExerciseInstructions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.StepOrder)
            .IsRequired();

        builder.HasIndex(x => new { x.ExerciseId, x.StepOrder })
            .IsUnique();
    }
}
