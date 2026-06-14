using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.UserModule.Application.Commands.Handlers;

public sealed class SetMyTimeZoneHandler(
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetMyTimeZoneCommand, Result>
{
    public async Task<Result> Handle(SetMyTimeZoneCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(NotFound("NotFound", "User profile not found."));

        user.SetTimeZone(request.TimeZoneId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
