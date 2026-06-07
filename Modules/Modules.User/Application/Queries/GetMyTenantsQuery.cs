using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.DTOs;

namespace Modules.UserModule.Application.Queries;

public record GetMyTenantsQuery : IRequest<Result<List<TenantDto>>>;
