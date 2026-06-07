using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.DTOs;

namespace Modules.UserModule.Application.Queries;

public record GetTenantMembersQuery(Guid TenantId) : IRequest<Result<List<MemberDto>>>;
