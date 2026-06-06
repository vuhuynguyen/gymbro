using Modules.UserModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Integration coverage for cross-tenant WRITE isolation (IntegrationTargets #1 — the write-path gap the
/// existing read tests left open). An Owner in one tenant must not be able to mutate another tenant's
/// resources. Two enforcement layers are exercised end to end against a seeded two-tenant Postgres:
///   • resources addressed by id (plans) — the EF global tenant filter hides the other tenant's row, so
///     the handler 404s even though the caller holds the static permission in their own tenant;
///   • tenant-parameter operations (member removal) — the per-request permission check denies, because
///     the caller has no role in the target tenant.
/// Drives the real MediatR pipeline (Validation → Authorization → handler). Skips without Docker.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CrossTenantWriteIsolationTests(PostgresFixture fixture)
{
    private async Task<Guid> CreatePlanInOtherTenantAsync()
    {
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var created = await fixture.SendAsync(
            new CreateWorkoutPlanCommand("Rival Program", null, null, null));
        Assert.True(created.IsSuccess);
        return created.Value;
    }

    [SkippableFact]
    public async Task Owner_cannot_update_a_plan_in_another_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var planId = await CreatePlanInOtherTenantAsync();

        // The caller holds PlanUpdate in their OWN tenant (the AuthorizationBehavior passes), but the EF
        // tenant filter hides the other tenant's plan, so the handler finds nothing → NotFound. No write.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(
            new UpdateWorkoutPlanCommand(planId, "Hijacked", null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task Owner_cannot_delete_a_plan_in_another_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var planId = await CreatePlanInOtherTenantAsync();

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new DeleteWorkoutPlanCommand(planId));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task Owner_cannot_remove_a_member_of_another_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // Caller is Owner in TenantId but targets OtherTenantId, where they hold no role — the handler's
        // ClientRemove permission check denies (Forbidden) before any membership lookup or mutation.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(
            new RemoveMemberCommand(fixture.OtherTenantId, fixture.OtherOwnerId));

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);
    }

    [SkippableFact]
    public async Task Owner_can_update_a_plan_in_their_own_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // Positive control: the same operation succeeds inside the caller's own tenant, proving the
        // failures above are tenant isolation rather than a blanket rejection.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var created = await fixture.SendAsync(
            new CreateWorkoutPlanCommand("My Program", null, null, null));
        Assert.True(created.IsSuccess);

        var result = await fixture.SendAsync(
            new UpdateWorkoutPlanCommand(created.Value, "My Program v2", null, null, null));

        Assert.True(result.IsSuccess);
    }
}
