using Modules.WorkoutSessionModule.Application.Queries;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Phase 1 unified personal training experience: the <c>api/me/*</c> read models aggregate the caller's
/// own data across all gyms via <c>QueryOwnAcrossGyms</c>, which deliberately bypasses the EF tenant
/// filter. These tests drive the real MediatR pipeline against the seeded fixture and assert the
/// guarantee that matters most once the filter is bypassed: a user can never see another user's data.
/// Skips automatically when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UnifiedPersonalTrainingTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task My_history_lists_only_the_callers_own_sessions()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(
            new GetMyWorkoutHistoryQuery(From: null, To: null, Status: null, Page: 1, PageSize: 50));

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientAId, s.TraineeId));
        Assert.Contains(result.Value!.Items, s => s.Id == fixture.SessionAId);
        Assert.DoesNotContain(result.Value!.Items, s => s.Id == fixture.SessionBId);
    }

    [SkippableFact]
    public async Task My_session_detail_returns_the_callers_own_session()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetMyWorkoutSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.SessionAId, result.Value!.Id);
        Assert.Equal(fixture.ClientAId, result.Value!.TraineeId);
    }

    [SkippableFact]
    public async Task My_session_detail_hides_another_users_session_as_not_found()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // Self-scoped: another user's session id simply doesn't resolve — NotFound, never a leak.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetMyWorkoutSessionByIdQuery(fixture.SessionBId));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [SkippableFact]
    public async Task My_progress_and_records_resolve_for_the_caller()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var progress = await fixture.SendAsync(new GetMyProgressQuery());
        var records = await fixture.SendAsync(new GetMyPersonalRecordsQuery());

        Assert.True(progress.IsSuccess);
        Assert.True(records.IsSuccess);
    }
}
