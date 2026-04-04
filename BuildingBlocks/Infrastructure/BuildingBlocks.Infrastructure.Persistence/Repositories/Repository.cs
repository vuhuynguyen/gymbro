using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.DomainPrimitives;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T>
    where T : AggregateRoot
{
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