using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Services;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Get-or-create semantics of <see cref="NutritionDayProvisioner"/>: an existing day is returned untouched;
/// with no assignment the active gym (<see cref="ITenantContext"/>) stamps a plan-less self-logged day; and a
/// caller with no tenant in context yields null (the write handler turns that into a clean failure). The write
/// surface is tenant-scoped, so the active gym is always present on the real path.
/// </summary>
public sealed class NutritionDayProvisionerTests
{
    private static readonly DateOnly Date = new(2026, 7, 4);

    private static NutritionDayProvisioner Build(
        IDailyNutritionLogRepository logRepo,
        ITenantContext tenantContext,
        INutritionPlanAssignmentRepository? assignments = null)
        => new(logRepo, assignments ?? Substitute.For<INutritionPlanAssignmentRepository>(),
            tenantContext, Substitute.For<IMediator>());

    [Fact]
    public async Task Existing_day_is_returned_unchanged()
    {
        var userId = Guid.NewGuid();
        var existing = DailyNutritionLog.OpenSelfLogged(userId, Guid.NewGuid(), Date, "UTC");
        var logRepo = Substitute.For<IDailyNutritionLogRepository>();
        logRepo.GetOwnByDateAsync(userId, Date, Arg.Any<CancellationToken>()).Returns(existing);
        var tenantContext = Substitute.For<ITenantContext>();

        var sut = Build(logRepo, tenantContext);
        var result = await sut.GetOrCreateForWriteAsync(userId, Date, "UTC", CancellationToken.None);

        Assert.Same(existing, result);
        await logRepo.DidNotReceive().AddAsync(Arg.Any<DailyNutritionLog>(), Arg.Any<CancellationToken>());
        // The existing-day fast path returns before the tenant is ever consulted.
        _ = tenantContext.DidNotReceive().TenantId;
    }

    [Fact]
    public async Task No_assignment_stamps_the_self_logged_day_with_the_active_gym()
    {
        var userId = Guid.NewGuid();
        var activeTenant = Guid.NewGuid();
        var logRepo = Substitute.For<IDailyNutritionLogRepository>();
        logRepo.GetOwnByDateAsync(userId, Date, Arg.Any<CancellationToken>()).Returns((DailyNutritionLog?)null);
        var assignments = Substitute.For<INutritionPlanAssignmentRepository>();
        assignments.QueryOwnAcrossGyms(userId).Returns(new TestAsyncEnumerable<NutritionPlanAssignment>(Array.Empty<NutritionPlanAssignment>()));
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(activeTenant);

        var sut = Build(logRepo, tenantContext, assignments);
        var result = await sut.GetOrCreateForWriteAsync(userId, Date, "UTC", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(NutritionSource.Adhoc, result!.Source);
        Assert.Equal(activeTenant, result.TenantId);
        Assert.Null(result.NutritionPlanAssignmentId);
        await logRepo.Received(1).AddAsync(result, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_assignment_and_no_tenant_in_context_returns_null()
    {
        var userId = Guid.NewGuid();
        var logRepo = Substitute.For<IDailyNutritionLogRepository>();
        logRepo.GetOwnByDateAsync(userId, Date, Arg.Any<CancellationToken>()).Returns((DailyNutritionLog?)null);
        var assignments = Substitute.For<INutritionPlanAssignmentRepository>();
        assignments.QueryOwnAcrossGyms(userId).Returns(new TestAsyncEnumerable<NutritionPlanAssignment>(Array.Empty<NutritionPlanAssignment>()));
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns((Guid?)null);

        var sut = Build(logRepo, tenantContext, assignments);
        var result = await sut.GetOrCreateForWriteAsync(userId, Date, "UTC", CancellationToken.None);

        Assert.Null(result);
        await logRepo.DidNotReceive().AddAsync(Arg.Any<DailyNutritionLog>(), Arg.Any<CancellationToken>());
    }
}
