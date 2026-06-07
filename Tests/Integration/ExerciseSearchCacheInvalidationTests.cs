using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Application.Queries;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Regression test for exercise search cache invalidation. Search results are cached under a generation
/// suffix; catalog mutations must bump the search generation so tenant reads see fresh rows.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExerciseSearchCacheInvalidationTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task Update_evicts_stale_search_results_for_a_non_admin_tenant_member()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        var tag = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"SearchCache {tag}";
        var renamedName = $"SearchCache {tag} renamed";
        var searchTerm = $"SearchCache {tag}";

        fixture.Principal.Become(Guid.NewGuid(), fixture.TenantId, isAdmin: true);
        var created = await fixture.SendAsync(NewExercise(originalName));
        Assert.True(created.IsSuccess);
        var exerciseId = created.Value;

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var beforeUpdate = await fixture.SendAsync(new SearchExercisesQuery(
            Search: searchTerm,
            MuscleGroup: null,
            Type: null,
            MovementType: null,
            Difficulty: null,
            Equipment: null));
        Assert.True(beforeUpdate.IsSuccess);
        Assert.Contains(beforeUpdate.Value!, e => e.Id == exerciseId && e.Name == originalName);

        fixture.Principal.Become(Guid.NewGuid(), fixture.TenantId, isAdmin: true);
        var updated = await fixture.SendAsync(RenameExercise(exerciseId, renamedName));
        Assert.True(updated.IsSuccess);

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var afterUpdate = await fixture.SendAsync(new SearchExercisesQuery(
            Search: searchTerm,
            MuscleGroup: null,
            Type: null,
            MovementType: null,
            Difficulty: null,
            Equipment: null));
        Assert.True(afterUpdate.IsSuccess);
        Assert.Contains(afterUpdate.Value!, e => e.Id == exerciseId && e.Name == renamedName);
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
