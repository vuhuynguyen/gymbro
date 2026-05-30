namespace BuildingBlocks.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a database transaction. The action should persist via
    /// <see cref="SaveChangesAsync"/> (or provider bulk APIs); the transaction is committed only after the action completes.
    /// </summary>
    Task ExecuteTransactionalAsync(Func<Task> action, CancellationToken cancellationToken = default);
}