using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
}
