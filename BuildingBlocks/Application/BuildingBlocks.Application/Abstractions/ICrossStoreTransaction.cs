namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Spans a single database transaction across the two EF Core stores that share one physical
/// database — the domain context (<see cref="IUnitOfWork"/>) and the Identity context — so that a
/// write touching both (registration, user deletion) commits or rolls back atomically. Without this
/// the two contexts commit independently and a second-store failure leaves an orphan (e.g. an
/// <c>AppUser</c> with no domain <c>User</c>, or vice-versa).
/// </summary>
public interface ICrossStoreTransaction
{
    /// <summary>
    /// Opens a transaction shared by both stores. Persist via the usual <see cref="IUnitOfWork"/> /
    /// Identity APIs while the scope is open, then call <see cref="ICrossStoreTransactionScope.CommitAsync"/>.
    /// Disposing without committing rolls back both stores.
    /// </summary>
    Task<ICrossStoreTransactionScope> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Handle for an open cross-store transaction. Commit to make both stores durable; dispose without
/// committing to roll both back.
/// </summary>
public interface ICrossStoreTransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
