using System.Linq.Expressions;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Entities;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;
using MediatR;
using BuildingBlocks.Shared.DomainPrimitives;
using Modules.ExerciseModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDbContextServices services)
    : DbContext(options), IUnitOfWork
{
    private readonly IPublisher _publisher = services.Publisher;
    public ICurrentUser CurrentUser { get; } = services.CurrentUser;

    public DbSet<Exercise> Exercises { get; set; } = null!;
    public DbSet<Translation> Translations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyGlobalFilters(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();
        
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOnUtc = utcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedOnUtc = utcNow;
                    break;
                case EntityState.Deleted:
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

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }

        return result;
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (!typeof(BaseEntity).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");

            Expression? finalExpression = null;

            // ========================
            // 1. Admin bypass
            // ========================
            var currentUserExpr = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentUser));

            var isAdminExpr = Expression.Property(
                currentUserExpr,
                nameof(ICurrentUser.IsAdmin));

            var isAdmin = Expression.Equal(isAdminExpr, Expression.Constant(true));

            // ========================
            // 2. Tenant / Shared logic
            // ========================
            Expression? tenantExpression = null;

            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                // e.TenantId == currentTenant
                tenantExpression = BuildTenantFilter(parameter, currentUserExpr);
            }
            else if (typeof(ISharedEntity).IsAssignableFrom(clrType))
            {
                // e.TenantId == null || e.TenantId == currentTenant
                tenantExpression = BuildSharedFilter(parameter, currentUserExpr);
            }

            // ========================
            // 3. Soft delete
            // ========================
            Expression? softDeleteExpression = null;

            if (typeof(ISoftDelete).IsAssignableFrom(clrType))
            {
                var isDeletedProp = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                softDeleteExpression = Expression.Equal(isDeletedProp, Expression.Constant(false));
            }

            // ========================
            // 4. Combine conditions
            // ========================
            Expression? combined = tenantExpression;

            if (combined != null && softDeleteExpression != null)
            {
                combined = Expression.AndAlso(combined, softDeleteExpression);
            }
            else if (combined == null)
            {
                combined = softDeleteExpression;
            }

            // ========================
            // 5. Admin override
            // ========================
            finalExpression = combined != null ? Expression.OrElse(isAdmin, combined) : isAdmin;

            var lambda = Expression.Lambda(finalExpression, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
    }
    
    private static Expression BuildSharedFilter(
        ParameterExpression parameter,
        MemberExpression currentUserExpr)
    {
        var tenantProperty = Expression.Property(parameter, nameof(ISharedEntity.TenantId));

        var tenantId = Expression.Property(
            currentUserExpr,
            nameof(ICurrentUser.TenantId));

        var isGlobal = Expression.Equal(tenantProperty, Expression.Constant(null));
        var isTenant = Expression.Equal(tenantProperty, tenantId);

        return Expression.OrElse(isGlobal, isTenant);
    }

    private static Expression BuildTenantFilter(
        ParameterExpression parameter,
        MemberExpression currentUserExpr)
    {
        var tenantProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));

        var tenantId = Expression.Property(
            currentUserExpr,
            nameof(ICurrentUser.TenantId));

        return Expression.Equal(tenantProperty, tenantId);
    }
}