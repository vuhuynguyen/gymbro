using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Infrastructure.Persistence.Configurations;

public class ExerciseMuscleConfiguration : IEntityTypeConfiguration<ExerciseMuscle>
{
    public void Configure(EntityTypeBuilder<ExerciseMuscle> builder)
    {
        builder.ToTable("ExerciseMuscles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Muscle)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.IsPrimary)
            .IsRequired();

        builder.HasIndex(x => new { x.ExerciseId, x.Muscle })
            .IsUnique();

        builder.HasIndex(x => x.ExerciseId);
    }
}
