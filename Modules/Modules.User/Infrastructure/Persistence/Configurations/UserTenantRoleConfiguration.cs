using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Infrastructure.Persistence.Configurations;

public class UserTenantRoleConfiguration : IEntityTypeConfiguration<UserTenantRole>
{
    public void Configure(EntityTypeBuilder<UserTenantRole> builder)
    {
        builder.ToTable("UserTenantRoles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(x => new { x.UserId, x.TenantId })
            .IsUnique();
    }
}
