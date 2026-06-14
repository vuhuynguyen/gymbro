using System.Linq.Expressions;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Outbox;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Shared.DomainPrimitives;

namespace BuildingBlocks.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDbContextServices services,
    IEnumerable<IModelConfiguration> modelConfigurations)
    : DbContext(options), IUnitOfWork
{
    public ICurrentUser CurrentUser { get; } = services.CurrentUser;
    public ITenantContext TenantContext { get; } = services.TenantContext;

    // The kernel owns NO module DbSets — module entity access goes through Set<T>() (in the module repositories)
    // and the model is assembled from injected IModelConfiguration contributors. This is what keeps the
    // persistence kernel free of any reference to a feature module (see OnModelCreating).

    // Transactional outbox: domain events are persisted here in the SAME transaction as the changes
    // that raised them (see SaveChangesAsync), then dispatched out-of-band by the OutboxProcessor.
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The model is CONTRIBUTED, not scanned here: each module supplies an IModelConfiguration (its own
        // entity configs); the composition root supplies the cross-module FK configs; the kernel supplies its
        // own (outbox). This is what lets the persistence kernel build the full model without referencing any
        // module — see CoreModelConfiguration and the per-module/cross-module contributors.
        foreach (var configuration in modelConfigurations)
            configuration.Apply(modelBuilder);

        ApplyGlobalFilters(modelBuilder);
    }

    public async Task ExecuteTransactionalAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action();
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        // Drain domain events into the outbox BEFORE saving so they commit atomically with the changes
        // that raised them. The OutboxProcessor then publishes them out-of-band (at-least-once), making
        // post-commit delivery durable and load-balancer-safe instead of at-most-once fire-and-forget.
        WriteDomainEventsToOutbox(utcNow);

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            var userId = CurrentUser.UserId;
            var hasUser = userId != Guid.Empty;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOnUtc = utcNow;
                    // Preserve a CreatedBy a factory already set; only stamp when none is present.
                    var hasCreator = entry.Entity.CreatedBy is { } creator && creator != Guid.Empty;
                    if (hasUser && !hasCreator)
                        entry.Property(nameof(BaseEntity.CreatedBy)).CurrentValue = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedOnUtc = utcNow;
                    if (hasUser)
                        entry.Property(nameof(BaseEntity.ModifiedBy)).CurrentValue = userId;
                    break;
                case EntityState.Deleted:
                    // Only aggregate roots that opt into ISoftDelete become soft-deletes.
                    // Child rows (e.g. ExerciseMuscle) use unique indexes that block re-insertion.
                    if (entry.Entity is not ISoftDelete)
                        break;

                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedOnUtc = utcNow;
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void WriteDomainEventsToOutbox(DateTime utcNow)
    {
        var entitiesWithEvents = ChangeTracker
            .Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            foreach (var domainEvent in entity.DomainEvents)
                OutboxMessages.Add(OutboxMessage.Create(domainEvent, utcNow));

            entity.ClearDomainEvents();
        }
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (!typeof(BaseEntity).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");

            // 1. Admin bypass (from ICurrentUser — identity concern)
            // INVARIANT: Expression.Constant(this) captures this DbContext instance in the compiled EF
            // query filter. The filter is safe because CurrentUser and TenantContext read from
            // IHttpContextAccessor.HttpContext on every call (AsyncLocal, not cached), so they always
            // return the value for the request that is currently executing — not the request that
            // constructed this instance. This guarantee BREAKS if either property is ever changed to
            // cache its value at construction time. AddDbContext (not AddDbContextPool) is used to
            // keep one instance per request and avoid cross-request confusion; see PersistenceExtensions.
            var currentUserExpr = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentUser));

            var isAdminExpr = Expression.Property(currentUserExpr, nameof(ICurrentUser.IsAdmin));
            var isAdmin = Expression.Equal(isAdminExpr, Expression.Constant(true));

            // 2. Tenant / Shared logic (from ITenantContext — location concern)
            var tenantContextExpr = Expression.Property(
                Expression.Constant(this),
                nameof(TenantContext));

            Expression? tenantExpression = null;

            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                tenantExpression = BuildTenantFilter(parameter, tenantContextExpr);
            }
            else if (typeof(ISharedEntity).IsAssignableFrom(clrType))
            {
                tenantExpression = BuildSharedFilter(parameter, tenantContextExpr);
            }

            // 3. Soft delete
            Expression? softDeleteExpression = null;

            if (typeof(ISoftDelete).IsAssignableFrom(clrType))
            {
                var isDeletedProp = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                softDeleteExpression = Expression.Equal(isDeletedProp, Expression.Constant(false));
            }

            // 4. Combine conditions
            Expression? combined = tenantExpression;

            if (combined != null && softDeleteExpression != null)
                combined = Expression.AndAlso(combined, softDeleteExpression);
            else if (combined == null)
                combined = softDeleteExpression;

            if (combined == null)
                continue;

            // Admin always bypasses all filters
            var finalExpression = Expression.OrElse(isAdmin, combined);
            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(finalExpression, parameter));
        }
    }

    private static Expression BuildSharedFilter(
        ParameterExpression parameter,
        MemberExpression tenantContextExpr)
    {
        var tenantProperty = Expression.Property(parameter, nameof(ISharedEntity.TenantId));
        var tenantId = Expression.Property(tenantContextExpr, nameof(ITenantContext.TenantId));

        var isGlobal = Expression.Equal(tenantProperty, Expression.Constant(null));
        var isTenant = Expression.Equal(tenantProperty, tenantId);

        return Expression.OrElse(isGlobal, isTenant);
    }

    private static Expression BuildTenantFilter(
        ParameterExpression parameter,
        MemberExpression tenantContextExpr)
    {
        var tenantProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
        var tenantId = Expression.Property(tenantContextExpr, nameof(ITenantContext.TenantId));

        return Expression.Equal(tenantProperty, tenantId);
    }
}
