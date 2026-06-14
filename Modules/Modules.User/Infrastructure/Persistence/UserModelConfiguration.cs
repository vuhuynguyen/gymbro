using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Modules.UserModule.Infrastructure.Persistence;

/// <summary>Contributes the User module's entity configurations (User, Tenant, UserTenantRole, Invite) to the
/// shared model. Cross-module FK configs live at the composition root.</summary>
public sealed class UserModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserModelConfiguration).Assembly);
}
