using BuildingBlocks.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Abstractions;

public interface ITenantRepository : IRepository<Tenant>
{
}
