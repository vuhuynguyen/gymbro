using Modules.WorkoutSessionModule.Application.Commands;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Integration coverage for IDOR on the session-mutation surface (IntegrationTargets #3). The terminal
/// transitions are gated by ownership (<c>session.TraineeId == caller</c>) and, across tenants, by the EF
/// global filter. These attacks all fail BEFORE any mutation, so the seeded sessions are left intact:
///   • a Client cannot abandon/complete another trainee's session → Unauthorized (ownership guard);
///   • an Owner of another tenant cannot touch the session at all → NotFound (tenant filter fires first).
/// Drives the real MediatR pipeline (the commands are WorkoutLogCreate-gated, so the behavior passes and
/// the handler's row-level guard is what must reject). Skips automatically without Docker.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionMutationIdorTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task Client_cannot_abandon_another_trainees_session()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // ClientB holds WorkoutLogCreate (so the AuthorizationBehavior passes), but SessionA belongs to
        // ClientA — the handler's ownership guard must reject the IDOR attempt.
        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);

        var result = await fixture.SendAsync(new AbandonSessionCommand(fixture.SessionAId, null));

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [SkippableFact]
    public async Task Client_cannot_complete_another_trainees_session()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);

        var result = await fixture.SendAsync(
            new CompleteSessionCommand(fixture.SessionAId, RpeOverall: null, Notes: null, CompletedAt: null));

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [SkippableFact]
    public async Task Owner_of_another_tenant_cannot_abandon_a_session()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // Different tenant: the EF tenant filter hides SessionA, so the handler 404s before the ownership
        // check even runs — defense in depth (filter + ownership).
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);

        var result = await fixture.SendAsync(new AbandonSessionCommand(fixture.SessionAId, null));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }
}
