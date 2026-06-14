using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Abstractions;

/// <summary>
/// A contributor to the shared <c>AppDbContext</c> model. Each feature module supplies one (applying its own
/// <see cref="IEntityTypeConfiguration{TEntity}"/> set); the composition root supplies the cross-module FK
/// configs. The persistence kernel applies the injected contributors in <c>OnModelCreating</c>, so it builds
/// the full model WITHOUT referencing any module — the model is contributed, never scanned from here.
/// </summary>
public interface IModelConfiguration
{
    void Apply(ModelBuilder modelBuilder);
}
