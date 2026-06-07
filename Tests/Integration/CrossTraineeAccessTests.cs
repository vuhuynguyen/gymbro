using Modules.WorkoutSessionModule.Application.Queries;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Integration coverage for the imperative-authorization seam: the workout-log
/// read endpoints are exempt from declarative <c>ITenantAuthorizedRequest</c> gating and instead do
/// row-level checks inside their handlers via <c>ResourceAccessGuard</c>. These tests drive the real
/// MediatR pipeline + EF global filters against a seeded two-trainee tenant and a second tenant, and
/// specifically attempt cross-trainee and cross-tenant reads through those exempt endpoints.
///
/// Covers IntegrationTargets items #1 (tenant isolation on reads) and #2 (ListSessions scoping S3).
/// Skips automatically when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CrossTraineeAccessTests(PostgresFixture fixture)
{
    private static ListSessionsQuery ListFor(Guid? traineeId) =>
        new(traineeId, From: null, To: null, Status: null, PlanAssignmentId: null, Page: 1, PageSize: 20);

    [SkippableFact]
    public async Task Client_listing_sessions_sees_only_their_own()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(ListFor(traineeId: null));

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientAId, s.TraineeId));
        Assert.Contains(result.Value!.Items, s => s.Id == fixture.SessionAId);
        Assert.DoesNotContain(result.Value!.Items, s => s.Id == fixture.SessionBId);
    }

    [SkippableFact]
    public async Task Client_cannot_read_another_trainee_by_supplying_TraineeId()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        // A Client lacks WorkoutLogViewAll, so the handler ignores the supplied TraineeId and scopes to
        // the caller — the IDOR attempt yields the caller's own (empty-of-B) list, never B's session.
        var result = await fixture.SendAsync(ListFor(traineeId: fixture.ClientBId));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value!.Items, s => s.Id == fixture.SessionBId);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientAId, s.TraineeId));
    }

    [SkippableFact]
    public async Task Client_cannot_read_another_trainees_session_by_id()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionBId));

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [SkippableFact]
    public async Task Client_can_read_their_own_session_by_id()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.SessionAId, result.Value!.Id);
        Assert.Equal(fixture.ClientAId, result.Value!.TraineeId);
    }

    [SkippableFact]
    public async Task Owner_with_ViewAll_can_read_a_trainees_session_by_id()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.ClientAId, result.Value!.TraineeId);
    }

    [SkippableFact]
    public async Task Owner_with_ViewAll_can_list_a_specific_trainees_sessions()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);

        var result = await fixture.SendAsync(ListFor(traineeId: fixture.ClientBId));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.Items, s => s.Id == fixture.SessionBId);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientBId, s.TraineeId));
    }

    [SkippableFact]
    public async Task Owner_of_another_tenant_cannot_read_a_session_via_global_filter()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // Same row, different tenant: the EF tenant filter hides it, so the handler 404s before any
        // row-level permission check even runs.
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }
}
