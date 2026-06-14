using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Infrastructure.Persistence.Configurations;

public class ExerciseTagConfiguration : IEntityTypeConfiguration<ExerciseTag>
{
    public void Configure(EntityTypeBuilder<ExerciseTag> builder)
    {
        builder.ToTable("ExerciseTags");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tag)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.ExerciseId, x.Tag })
            .IsUnique();
    }
}
