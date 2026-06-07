using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.DTOs;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.UserModule.Application.Queries.Handlers;

public sealed class GetMeHandler(
    IUserRepository userRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMeQuery, Result<MeDto>>
{
    public async Task<Result<MeDto>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, cancellationToken);
        if (user is null)
            return Result<MeDto>.Failure(NotFound("NotFound", "User not found."));

        return Result<MeDto>.Success(
            new MeDto(
                currentUser.UserId,
                user.Name,
                request.EmailFromClaims,
                currentUser.IsAdmin));
    }
}
