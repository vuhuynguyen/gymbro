using BuildingBlocks.Shared.DomainPrimitives;

namespace BuildingBlocks.Application.Abstractions;

public interface IRepository<T> where T : AggregateRoot
{
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Remove(T entity);
    IQueryable<T> Query();
}