using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Infrastructure.Persistence;

/// <summary>
/// Spans one transaction across the domain context (<see cref="IUnitOfWork"/>, i.e. <c>AppDbContext</c>)
/// and <see cref="IdentityDbContext"/>. Both target the same physical PostgreSQL database (same
/// connection string), so a single connection + transaction can cover them: we point Identity at the
/// domain context's connection and enlist both contexts in one transaction. This is the only assembly
/// that legitimately references both stores, so the coordination lives here.
/// </summary>
internal sealed class CrossStoreTransaction(
    IUnitOfWork unitOfWork,
    IdentityDbContext identityDb) : ICrossStoreTransaction
{
    public async Task<ICrossStoreTransactionScope> BeginAsync(CancellationToken cancellationToken = default)
    {
        // The domain unit of work is always an EF Core DbContext (AppDbContext) — we need its
        // DatabaseFacade to share the connection and own the transaction.
        if (unitOfWork is not DbContext appDb)
            throw new InvalidOperationException(
                $"{nameof(CrossStoreTransaction)} requires {nameof(IUnitOfWork)} to be an EF Core DbContext.");

        // Point Identity at the domain context's connection so both share one physical connection.
        // Safe because Identity's own connection is closed at this point (BeginAsync is called before
        // any Identity write in the flow); contextOwnsConnection: false keeps AppDbContext the owner.
        var connection = appDb.Database.GetDbConnection();
        identityDb.Database.SetDbConnection(connection, contextOwnsConnection: false);

        var transaction = await appDb.Database.BeginTransactionAsync(cancellationToken);
        await identityDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

        return new Scope(transaction, identityDb);
    }

    // The transaction is owned by the domain context; the Identity context only borrows it via
    // UseTransaction. Committing/rolling back the owner does NOT clear the borrower's transaction
    // reference, so we detach it explicitly — otherwise a later write on the Identity context within the
    // same request (e.g. refresh-token issuance after registration) would enlist in a completed transaction.
    private sealed class Scope(IDbContextTransaction transaction, DbContext secondary) : ICrossStoreTransactionScope
    {
        private bool _completed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
            await secondary.Database.UseTransactionAsync(null, cancellationToken);
            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            // Rolls back if the caller threw or returned without committing — both stores revert together.
            if (!_completed)
            {
                await transaction.RollbackAsync();
                await secondary.Database.UseTransactionAsync(null);
            }

            await transaction.DisposeAsync();
        }
    }
}
