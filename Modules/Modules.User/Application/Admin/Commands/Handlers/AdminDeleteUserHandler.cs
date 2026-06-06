using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Admin.Commands.Handlers;

public class AdminDeleteUserHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    ICrossStoreTransaction crossStoreTransaction)
    : IRequestHandler<AdminDeleteUserCommand, Result>
{
    public async Task<Result> Handle(AdminDeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.Failure(Error.NotFound("User not found."));

        // The domain User (DB1) soft-delete and the Identity AppUser (DB3) hard-delete (done by the
        // UserDeletedNotification handler) live in separate contexts on the same database. Wrap both in
        // one cross-store transaction so they commit or roll back together — a failed Identity cleanup
        // rolls the domain delete back rather than leaving an orphaned AppUser.
        await using var transaction = await crossStoreTransaction.BeginAsync(cancellationToken);

        userRepository.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await mediator.Publish(new UserDeletedNotification(request.UserId), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
