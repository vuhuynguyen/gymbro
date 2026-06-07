using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public class TenantRepository(AppDbContext context) : Repository<Tenant>(context), ITenantRepository
{
}
