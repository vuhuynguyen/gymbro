using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Application.Queries;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Regression test for the exercise-detail cache-invalidation bug. GetExerciseById caches the detail
/// DTO under a per-tenant scope key — one entry per tenant that has viewed the exercise, plus an
/// "admin" entry. Update/Delete used to evict only the "admin" entry, so every per-tenant entry kept
/// serving stale detail until its 2-minute TTL lapsed. Both handlers now trip
/// <c>ExerciseDetailCacheSignal</c>, which evicts every scoped entry at once (mirroring the search cache).
///
/// Each test reads the exercise as a NON-ADMIN tenant member (populating that tenant's detail entry),
/// mutates it as the platform admin, then re-reads as the same tenant member and asserts the result is
/// fresh — i.e. the pre-mutation cached value is no longer served. Before the fix these reads returned
/// the stale snapshot; both assertions would fail.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExerciseDetailCacheInvalidationTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task Update_evicts_stale_detail_for_a_non_admin_tenant_member()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // Unique names keep the test rerunnable against a persistent (non-throwaway) database.
        var tag = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"Cable Fly {tag}";
        var renamedName = $"Cable Fly {tag} (renamed)";

        // Admin creates a global catalog exercise.
        fixture.Principal.Become(Guid.NewGuid(), fixture.TenantId, isAdmin: true);
        var created = await fixture.SendAsync(NewExercise(originalName));
        Assert.True(created.IsSuccess, "admin should be able to create an exercise");
        var exerciseId = created.Value;

        // A non-admin tenant member (Owner role → PlanView) reads the detail, populating the
        // tenant-scoped cache entry exercise:detail:{id}:{tenantId}.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var beforeUpdate = await fixture.SendAsync(new GetExerciseByIdQuery(exerciseId));
        Assert.True(beforeUpdate.IsSuccess);
        Assert.Equal(originalName, beforeUpdate.Value!.Name);

        // Admin renames the exercise.
        fixture.Principal.Become(Guid.NewGuid(), fixture.TenantId, isAdmin: true);
        var updated = await fixture.SendAsync(RenameExercise(exerciseId, renamedName));
        Assert.True(updated.IsSuccess, "admin should be able to update an exercise");

        // The same tenant member reads again: must see the new name, not the stale cached one.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var afterUpdate = await fixture.SendAsync(new GetExerciseByIdQuery(exerciseId));
        Assert.True(afterUpdate.IsSuccess);
        Assert.Equal(renamedName, afterUpdate.Value!.Name);
    }

    [SkippableFact]
    public async Task Delete_evicts_stale_detail_for_a_non_admin_tenant_member()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        var tag = Guid.NewGuid().ToString("N")[..8];
        var name = $"Pec Deck {tag}";

        // Admin creates a global catalog exercise.
        fixture.Principal.Become(Guid.NewGuid(), fixture.TenantId, isAdmin: true);
        var created = await fixture.SendAsync(NewExercise(name));
        Assert.True(created.IsSuccess);
        var exerciseId = created.Value;

        // Non-admin tenant member populates the tenant-scoped detail entry.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var beforeDelete = await fixture.SendAsync(new GetExerciseByIdQuery(exerciseId));
        Assert.True(beforeDelete.IsSuccess);

        // Admin soft-deletes the exercise.
        fixture.Principal.Become(Guid.NewGuid(), fixture.TenantId, isAdmin: true);
        var deleted = await fixture.SendAsync(new DeleteExerciseCommand(exerciseId));
        Assert.True(deleted.IsSuccess, "admin should be able to delete an exercise");

        // The same tenant member reads again: the stale entry must be gone, so the now soft-deleted
        // exercise resolves to NotFound instead of serving the pre-delete snapshot.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var afterDelete = await fixture.SendAsync(new GetExerciseByIdQuery(exerciseId));
        Assert.True(
            afterDelete.IsFailure,
            "deleted exercise must not be served from a stale tenant cache entry");
        Assert.Equal("NotFound", afterDelete.Error.Code);
    }

    private static CreateExerciseCommand NewExercise(string name) => new(
        Name: name,
        Description: "Cache invalidation test fixture exercise.",
        Type: "Strength",
        MovementType: "Isolation",
        Difficulty: "Beginner",
        Equipment: "Machine",
        EstimatedCaloriesBurn: 50,
        AverageDurationSeconds: 60,
        ImageUrl: null,
        Muscles: new[] { new ExerciseMuscleInput("Chest", true) },
        Instructions: new[] { "Set up at the machine", "Squeeze the handles together" },
        Tags: new[] { "isolation" },
        Media: null,
        Warnings: null);

    private static UpdateExerciseCommand RenameExercise(Guid id, string name) => new(
        ExerciseId: id,
        Name: name,
        Description: "Cache invalidation test fixture exercise.",
        Type: "Strength",
        MovementType: "Isolation",
        Difficulty: "Beginner",
        Equipment: "Machine",
        EstimatedCaloriesBurn: 50,
        AverageDurationSeconds: 60,
        ImageUrl: null,
        Muscles: new[] { new ExerciseMuscleInput("Chest", true) },
        Instructions: new[] { "Set up at the machine", "Squeeze the handles together" },
        Tags: new[] { "isolation" },
        Media: null,
        Warnings: null);
}
