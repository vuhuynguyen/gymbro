using System.Linq.Expressions;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Entities;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;
using MediatR;
using BuildingBlocks.Shared.DomainPrimitives;
using Modules.ExerciseModule.Entities;
using Modules.UserModule.Entities;
using Modules.WorkoutPlanModule.Entities;
using Modules.WorkoutSessionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDbContextServices services)
    : DbContext(options), IUnitOfWork
{
    private readonly IPublisher _publisher = services.Publisher;
    public ICurrentUser CurrentUser { get; } = services.CurrentUser;
    public ITenantContext TenantContext { get; } = services.TenantContext;

    public DbSet<Exercise> Exercises { get; set; } = null!;
    public DbSet<Translation> Translations { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<UserTenantRole> UserTenantRoles { get; set; } = null!;
    public DbSet<Invite> Invites { get; set; } = null!;
    public DbSet<WorkoutPlan> WorkoutPlans { get; set; } = null!;
    public DbSet<PlanAssignment> PlanAssignments { get; set; } = null!;
    public DbSet<WorkoutSession> WorkoutSessions { get; set; } = null!;
    public DbSet<PerformedExercise> PerformedExercises { get; set; } = null!;
    public DbSet<PerformedSet> PerformedSets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

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
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            var userId = CurrentUser.UserId;
            var hasUser = userId != Guid.Empty;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOnUtc = utcNow;
                    if (hasUser)
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

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }

        foreach (var entry in ChangeTracker.Entries<Entity>())
            entry.Entity.ClearDomainEvents();

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

            // ========================
            // 1. Admin bypass (from ICurrentUser — identity concern)
            // ========================
            var currentUserExpr = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentUser));

            var isAdminExpr = Expression.Property(currentUserExpr, nameof(ICurrentUser.IsAdmin));
            var isAdmin = Expression.Equal(isAdminExpr, Expression.Constant(true));

            // ========================
            // 2. Tenant / Shared logic (from ITenantContext — location concern)
            // ========================
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
