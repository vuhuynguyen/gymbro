using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// The persistence kernel's own model contributor — applies the configurations that live in this project (the
/// transactional <c>OutboxMessage</c>). Module entity configs are contributed by each module's own
/// <see cref="IModelConfiguration"/>; cross-module FK configs come from the composition root.
/// </summary>
public sealed class CoreModelConfiguration : IModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreModelConfiguration).Assembly);
}
