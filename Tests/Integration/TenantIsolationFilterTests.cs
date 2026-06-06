using Modules.WorkoutSessionModule.Application.Queries;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Verifies the tenant-isolation invariant that is protected by AppDbContext.ApplyGlobalFilters:
/// the EF query filter uses Expression.Constant(this) to capture the DbContext instance, and reads
/// CurrentUser.TenantId live from IHttpContextAccessor (AsyncLocal) on every query. These tests
/// confirm that switching the acting principal between two requests correctly narrows or blocks
/// what each request sees — i.e., the filter re-evaluates per-call and does not bake in the tenant
/// from the first request that touched the DbContext instance.
///
/// If this invariant ever broke (e.g. CurrentUser cached TenantId at construction time), both tests
/// would fail: the cross-tenant read would succeed instead of returning NotFound.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantIsolationFilterTests(PostgresFixture fixture)
{
    private static GetSessionByIdQuery GetSession(Guid id) => new(id);

    [SkippableFact]
    public async Task Same_session_visible_for_correct_tenant_and_hidden_for_other_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // Request 1 — correct tenant: session should be found.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var hit = await fixture.SendAsync(GetSession(fixture.SessionAId));
        Assert.True(hit.IsSuccess, "Expected session to be visible to its owner in the correct tenant");
        Assert.Equal(fixture.SessionAId, hit.Value!.Id);

        // Request 2 — different tenant, same DbContext instance: the filter must re-evaluate
        // with the new principal's TenantId and hide the row.
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var miss = await fixture.SendAsync(GetSession(fixture.SessionAId));
        Assert.True(miss.IsFailure, "Expected session to be invisible to a user from a different tenant");
        Assert.Equal("NotFound", miss.Error.Code);
    }

    [SkippableFact]
    public async Task Switching_back_to_original_tenant_restores_visibility()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // Alternate back and forth to confirm the filter is truly per-call, not cached.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var first = await fixture.SendAsync(GetSession(fixture.SessionAId));
        Assert.True(first.IsSuccess);

        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var hidden = await fixture.SendAsync(GetSession(fixture.SessionAId));
        Assert.True(hidden.IsFailure);

        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var restored = await fixture.SendAsync(GetSession(fixture.SessionAId));
        Assert.True(restored.IsSuccess, "Visibility should be restored when switching back to the original tenant");
    }
}
