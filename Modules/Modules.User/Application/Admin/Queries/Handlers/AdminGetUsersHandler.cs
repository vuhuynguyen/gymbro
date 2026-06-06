using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.DTOs;
using Modules.UserModule.Application.Mapping;

namespace Modules.UserModule.Application.Admin.Queries.Handlers;

public class AdminGetUsersHandler(
    IUserRepository userRepository)
    : IRequestHandler<AdminGetUsersQuery, Result<List<AdminUserDto>>>
{
    public async Task<Result<List<AdminUserDto>>> Handle(
        AdminGetUsersQuery request, CancellationToken cancellationToken)
    {
        // Bound the result set. Defaults keep the response shape (a bare list) backward compatible
        // with existing callers while clamping the page size so the whole table can never be loaded.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 200);

        var users = await userRepository.Query()
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(UserMapping.AdminUserProjection)
            .ToListAsync(cancellationToken);

        return Result<List<AdminUserDto>>.Success(users);
    }
}
