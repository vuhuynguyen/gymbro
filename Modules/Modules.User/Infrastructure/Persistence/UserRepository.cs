using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Infrastructure.Persistence;

public class UserRepository(DbContext context) : Repository<User>(context), IUserRepository
{
}
