using Modules.UserModule.Application.Commands;
using Modules.UserModule.Application.Queries;
using Modules.WorkoutPlanModule.Application;
using Modules.WorkoutPlanModule.Application.Commands;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Cross-tenant write-path isolation for
/// <c>PlanAssignment</c> (an ITenantEntity, hidden by the EF global filter) and <c>Invite</c> (NOT
/// tenant-filtered — the handler must scope by tenant itself). Also asserts the authorization layer fails
/// closed when the resolved tenant names a tenant the caller is not a member of — the defense-in-depth
/// guarantee behind <c>TenantResolutionMiddleware</c>'s membership check.
///
/// Drives the real MediatR pipeline (Validation → Authorization → handler) against a seeded two-tenant
/// Postgres. Skips when Docker is unavailable.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CrossTenantAssignmentAndInviteIsolationTests(PostgresFixture fixture)
{
    private static readonly DateOnly StartDate = new(2026, 1, 1);

    private async Task<Guid> CreatePlanInOtherTenantAsync(string name)
    {
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var created = await fixture.SendAsync(new CreateWorkoutPlanCommand(name, null, null, null));
        Assert.True(created.IsSuccess);
        return created.Value;
    }

    private async Task<Guid> CreateSelfAssignmentInOtherTenantAsync()
    {
        var planId = await CreatePlanInOtherTenantAsync("Rival Plan");
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var created = await fixture.SendAsync(new CreatePlanAssignmentCommand(
            fixture.OtherOwnerId, planId, StartDate, 3,
            PlanVisibilityMode.Full, false, false, false, false, null));
        Assert.True(created.IsSuccess);
        return created.Value;
    }

    [SkippableFact]
    public async Task Owner_cannot_create_an_assignment_referencing_another_tenants_plan()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var otherPlanId = await CreatePlanInOtherTenantAsync("Foreign Plan");

        // Caller holds PlanAssign in their OWN tenant (AuthorizationBehavior passes), but the plan id
        // belongs to OtherTenant; the EF tenant filter hides it, so the handler 404s — no assignment made.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new CreatePlanAssignmentCommand(
            fixture.ClientAId, otherPlanId, StartDate, 3,
            PlanVisibilityMode.Full, false, false, false, false, null));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task Owner_cannot_update_an_assignment_in_another_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var assignmentId = await CreateSelfAssignmentInOtherTenantAsync();

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new UpdatePlanAssignmentCommand(
            assignmentId, new DateOnly(2026, 2, 1), 5,
            PlanVisibilityMode.Full, false, false, false, false));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task Owner_cannot_delete_an_assignment_in_another_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var assignmentId = await CreateSelfAssignmentInOtherTenantAsync();

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new DeletePlanAssignmentCommand(assignmentId));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task Owner_cannot_revoke_an_invite_of_another_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // Generate an invite inside OtherTenant...
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var generated = await fixture.SendAsync(new GenerateInviteCommand());
        Assert.True(generated.IsSuccess);

        // ...then try to revoke it from the caller's OWN tenant. Invite has no global tenant filter, so
        // RevokeInviteHandler must scope via GetByCodeAndTenantAsync — a foreign code resolves to nothing.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var result = await fixture.SendAsync(new RevokeInviteCommand(generated.Value!));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task Listing_invites_returns_only_the_callers_tenant()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var foreign = await fixture.SendAsync(new GenerateInviteCommand());
        Assert.True(foreign.IsSuccess);

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var mine = await fixture.SendAsync(new GenerateInviteCommand());
        Assert.True(mine.IsSuccess);

        var listed = await fixture.SendAsync(new GetTenantInvitesQuery());

        Assert.True(listed.IsSuccess);
        Assert.Contains(listed.Value!, i => i.Code == mine.Value);
        Assert.DoesNotContain(listed.Value!, i => i.Code == foreign.Value);
    }

    [SkippableFact]
    public async Task Authorization_fails_closed_when_the_resolved_tenant_is_not_the_callers()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // Simulates a spoofed/foreign X-Tenant-Id that somehow reached the context: ClientA is a member of
        // TenantId only, yet the resolved tenant is OtherTenantId. AuthorizationBehavior resolves the
        // caller's role in OtherTenantId from the DB (null) and denies — even before the EF filter would
        // hide the rows. (TenantResolutionMiddleware would normally never set a non-member tenant; this is
        // the second line of defense — see TenantResolutionMiddlewareTests for the first.)
        fixture.Principal.Become(fixture.ClientAId, fixture.OtherTenantId);
        var result = await fixture.SendAsync(new GetTenantInvitesQuery());

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }
}
