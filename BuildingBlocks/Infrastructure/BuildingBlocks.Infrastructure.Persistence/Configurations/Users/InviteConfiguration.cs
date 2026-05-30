using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.UserModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Configurations.Users;

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("Invites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(8)
            .IsFixedLength();

        builder.HasIndex(x => x.Code)
            .HasFilter("\"IsUsed\" = false")
            .IsUnique();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ExpiredAt)
            .IsRequired();

        builder.Property(x => x.IsUsed)
            .HasDefaultValue(false);

        // Only one active invite per email+tenant (for email-based invites)
        builder.HasIndex(x => new { x.Email, x.TenantId })
            .HasFilter("\"IsUsed\" = false AND \"Email\" IS NOT NULL")
            .IsUnique();
    }
}
