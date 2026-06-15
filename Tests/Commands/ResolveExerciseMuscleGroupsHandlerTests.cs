using BuildingBlocks.Shared.Tracking;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Queries;
using Modules.ExerciseModule.Application.Queries.Handlers;
using Modules.ExerciseModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The cross-module muscle-group resolver (ResolveExerciseMuscleGroupsQuery), fully mocked — no database.
/// Pins the boundary-safe contract: the PRIMARY muscle group is returned per id as a camelCase STRING (one of
/// chest|back|legs|shoulders|arms|core), so the value can cross into WorkoutSession without the consumer ever
/// touching the MuscleGroup entity enum. The repository's Query() is fed a plain list through the in-memory
/// async provider (AsNoTracking is a no-op off an EF provider); an empty id set short-circuits and an unknown
/// id is simply absent from the map.
/// </summary>
public sealed class ResolveExerciseMuscleGroupsHandlerTests
{
    private static ResolveExerciseMuscleGroupsHandler CreateSut(IEnumerable<Exercise> exercises)
    {
        var repo = Substitute.For<IExerciseRepository>();
        repo.Query().Returns(new TestAsyncEnumerable<Exercise>(exercises));
        return new ResolveExerciseMuscleGroupsHandler(repo);
    }

    private static Exercise ExerciseWithMuscles(
        params (MuscleGroup muscle, bool isPrimary)[] muscles)
        => Exercise.CreateGlobal(
            name: "Lift",
            imageUrl: "",
            description: "",
            type: ExerciseType.Strength,
            movementType: MovementType.Compound,
            difficulty: DifficultyLevel.Intermediate,
            equipment: Equipment.Barbell,
            estimatedCaloriesBurn: null,
            averageDurationSeconds: null,
            muscles: muscles,
            trackingType: ExerciseTrackingType.Strength);

    [Fact]
    public async Task Returns_the_primary_group_per_id_as_a_camelCase_string()
    {
        // Bench → primary Chest (secondary Arms); Squat → primary Legs. The map must carry the PRIMARY group,
        // lower-cased, not the secondary.
        var bench = ExerciseWithMuscles((MuscleGroup.Chest, true), (MuscleGroup.Arms, false));
        var squat = ExerciseWithMuscles((MuscleGroup.Legs, true));

        var sut = CreateSut([bench, squat]);
        var result = await sut.Handle(
            new ResolveExerciseMuscleGroupsQuery([bench.Id, squat.Id]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var map = result.Value!;
        Assert.Equal("chest", map[bench.Id]);
        Assert.Equal("legs", map[squat.Id]);
    }

    [Fact]
    public async Task Covers_all_six_groups_in_camelCase()
    {
        var byGroup = new[]
        {
            (MuscleGroup.Chest, "chest"),
            (MuscleGroup.Back, "back"),
            (MuscleGroup.Legs, "legs"),
            (MuscleGroup.Shoulders, "shoulders"),
            (MuscleGroup.Arms, "arms"),
            (MuscleGroup.Core, "core"),
        };

        var exercises = byGroup
            .Select(g => ExerciseWithMuscles((g.Item1, true)))
            .ToList();

        var sut = CreateSut(exercises);
        var result = await sut.Handle(
            new ResolveExerciseMuscleGroupsQuery(exercises.Select(e => e.Id).ToList()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var map = result.Value!;
        for (var i = 0; i < exercises.Count; i++)
            Assert.Equal(byGroup[i].Item2, map[exercises[i].Id]);
    }

    [Fact]
    public async Task Empty_id_set_returns_empty_map_without_querying()
    {
        var repo = Substitute.For<IExerciseRepository>();
        var sut = new ResolveExerciseMuscleGroupsHandler(repo);

        var result = await sut.Handle(
            new ResolveExerciseMuscleGroupsQuery([]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
        repo.DidNotReceive().Query();
    }

    [Fact]
    public async Task Unknown_id_is_absent_from_the_map()
    {
        var known = ExerciseWithMuscles((MuscleGroup.Back, true));
        var unknown = Guid.NewGuid();

        var sut = CreateSut([known]);
        var result = await sut.Handle(
            new ResolveExerciseMuscleGroupsQuery([known.Id, unknown]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var map = result.Value!;
        Assert.Equal("back", map[known.Id]);
        Assert.False(map.ContainsKey(unknown));
    }
}
