using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.DomainPrimitives;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Abstractions;

/// <summary>
/// Generic aggregate repository. Depends only on <see cref="DbContext"/> (not the concrete app context) so it
/// can live in the persistence kernel and be reused by every module without inverting the dependency direction.
/// At runtime DI maps <see cref="DbContext"/> to the single app context.
/// </summary>
public class Repository<T>(DbContext context) : IRepository<T>
    where T : AggregateRoot
{
    protected DbContext Db => context;

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await context.Set<T>().AddAsync(entity, cancellationToken);
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<T>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public void Remove(T entity)
    {
        context.Set<T>().Remove(entity);
    }

    public IQueryable<T> Query()
    {
        return context.Set<T>().AsQueryable();
    }
}
