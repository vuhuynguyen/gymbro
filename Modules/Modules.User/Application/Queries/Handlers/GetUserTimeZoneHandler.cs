using BuildingBlocks.Application.Messaging;
using MediatR;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Queries.Handlers;

/// <summary>Returns the user's stored IANA time zone (the authoritative anchor) for the Identity module to stamp
/// into the access token's <c>tz</c> claim. Null if the user is missing or hasn't set one.</summary>
public sealed class GetUserTimeZoneHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserTimeZoneQuery, string?>
{
    public async Task<string?> Handle(GetUserTimeZoneQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user?.TimeZoneId;
    }
}
