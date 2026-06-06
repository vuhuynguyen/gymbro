using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.DTOs;

namespace Modules.UserModule.Application.Admin.Queries;

public record AdminGetUsersQuery(int Page = 1, int PageSize = 50)
    : IRequest<Result<List<AdminUserDto>>>, IPlatformAdminRequest;
