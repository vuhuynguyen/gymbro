using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.DTOs;
using Modules.UserModule.Application.Mapping;

namespace Modules.UserModule.Application.Queries.Handlers;

public class GetTenantInvitesHandler(
    IInviteRepository inviteRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetTenantInvitesQuery, Result<List<InviteCodeDto>>>
{
    public async Task<Result<List<InviteCodeDto>>> Handle(
        GetTenantInvitesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var invites = await inviteRepository.GetByTenantAsync(tenantId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var result = invites
            .Where(i => i.Email == null)  // only code-based invites
            .Select(i => UserMapping.ToInviteCodeDto(i, now))
            .ToList();

        return Result<List<InviteCodeDto>>.Success(result);
    }
}
