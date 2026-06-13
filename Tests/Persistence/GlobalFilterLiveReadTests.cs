using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using Gymbro.Tests.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Entities;
using Xunit;

namespace Gymbro.Tests.Persistence;

/// <summary>
/// Locks the load-bearing invariant behind <c>AppDbContext.ApplyGlobalFilters</c>: the
/// global query filter captures the DbContext via <c>Expression.Constant(this)</c> and reads
/// <c>CurrentUser.IsAdmin</c> / <c>TenantContext.TenantId</c> <b>live on every query</b>, which is why the
/// context must be registered per-request (<c>AddDbContext</c>, never <c>AddDbContextPool</c>). If anyone ever
/// makes those properties cache their value at construction, tenant isolation and the admin bypass would bake
/// in the FIRST request's principal and silently leak across tenants.
///
/// <para><c>TenantIsolationFilterTests</c> already pins the <b>tenant</b> dimension end-to-end against Postgres.
/// This complements it by pinning the <b>admin-bypass</b> dimension on a single context instance, and runs on
/// the EF InMemory provider so it executes everywhere (no Docker/Postgres gate).</para>
/// </summary>
public sealed class GlobalFilterLiveReadTests
{
    [Fact]
    public async Task Admin_bypass_re_evaluates_live_on_the_same_context_instance()
    {
        var principal = new TestPrincipal();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"global-filter-live-{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, new MutablePrincipalServices(principal));

        var tenantA = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();

        // Seed a tenant-owned, soft-deletable plan for tenant A (inserts are never filtered).
        principal.Become(Guid.NewGuid(), tenantA);
        db.WorkoutPlans.Add(
            WorkoutPlan.Create(tenantA, Guid.NewGuid(), "Plan A", null, durationWeeks: 4, workoutsPerWeek: 3));
        await db.SaveChangesAsync();

        // Same instance, a DIFFERENT tenant, non-admin: the tenant filter hides the row.
        principal.Become(Guid.NewGuid(), otherTenant, isAdmin: false);
        Assert.Empty(await db.WorkoutPlans.AsNoTracking().ToListAsync());

        // Same instance, flip IsAdmin ON: the admin-bypass branch must re-read live and reveal the row.
        principal.Become(Guid.NewGuid(), otherTenant, isAdmin: true);
        Assert.Single(await db.WorkoutPlans.AsNoTracking().ToListAsync());

        // Same instance, flip IsAdmin back OFF: hidden again — proves per-query evaluation, not a cached value.
        principal.Become(Guid.NewGuid(), otherTenant, isAdmin: false);
        Assert.Empty(await db.WorkoutPlans.AsNoTracking().ToListAsync());

        // And the owning tenant still sees it without admin (sanity: the bypass isn't the only path).
        principal.Become(Guid.NewGuid(), tenantA, isAdmin: false);
        Assert.Single(await db.WorkoutPlans.AsNoTracking().ToListAsync());
    }

    /// <summary>Wraps the mutable <see cref="TestPrincipal"/> so the EF filters read whatever it currently holds.</summary>
    private sealed class MutablePrincipalServices(TestPrincipal principal) : IDbContextServices
    {
        public ICurrentUser CurrentUser => principal;
        public ITenantContext TenantContext => principal;
    }
}
