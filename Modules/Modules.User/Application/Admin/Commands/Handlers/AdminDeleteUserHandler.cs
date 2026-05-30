using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Admin.Commands.Handlers;

public class AdminDeleteUserHandler(
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IMediator mediator)
    : IRequestHandler<AdminDeleteUserCommand, Result>
{
    public async Task<Result> Handle(AdminDeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (AdminPolicy.Deny(currentUser) is { } denied)
            return denied;

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.Failure(Error.NotFound("User not found."));

        userRepository.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Cross-store cleanup: the domain User and Identity AppUser live in separate
        // contexts (no FK). Notify Identity to delete the orphaned AppUser (DB3).
        await mediator.Publish(new UserDeletedNotification(request.UserId), cancellationToken);

        return Result.Success();
    }
}
