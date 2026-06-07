using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Shared.Results;
using Microsoft.Extensions.Caching.Distributed;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Application.Commands.Handlers;
using Modules.ExerciseModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the global-exercise creation guards: a name that already exists in the catalog is rejected with
/// <c>Conflict</c> before any insert, an unparseable enum (e.g. exercise type) is rejected with
/// <c>Validation</c>, and the happy path builds the aggregate from the command, persists it via the
/// repository plus a single SaveChanges, and busts the search cache. Fully mocked — no database.
/// (<see cref="ExerciseCatalogCache"/> is sealed, so it is constructed with substituted dependencies;
/// the handler only touches its <c>InvalidateSearchAsync</c> path, which delegates to the generation counter.)
/// </summary>
public sealed class CreateExerciseHandlerTests
{
    private static CreateExerciseHandler CreateSut(
        IExerciseRepository repository,
        IUnitOfWork unitOfWork,
        ICacheGenerationCounter generations)
    {
        var catalogCache = new ExerciseCatalogCache(
            Substitute.For<IDistributedCache>(),
            Substitute.For<ICacheKeyNamespace>(),
            generations,
            new ExerciseCatalogSearchReader(Substitute.For<IExerciseRepository>()),
            new ExerciseCatalogDetailReader(Substitute.For<IExerciseRepository>()));

        return new CreateExerciseHandler(repository, unitOfWork, catalogCache);
    }

    private static CreateExerciseCommand ValidCommand(string name = "Barbell Bench Press") =>
        new(
            Name: name,
            Description: "Press a barbell from the chest.",
            Type: "Strength",
            MovementType: "Compound",
            Difficulty: "Intermediate",
            Equipment: "Barbell",
            EstimatedCaloriesBurn: 50,
            AverageDurationSeconds: 120,
            ImageUrl: null,
            Muscles: new[] { new ExerciseMuscleInput("Chest", true) },
            Instructions: null,
            Tags: null,
            Media: null,
            Warnings: null);

    [Fact]
    public async Task Existing_name_is_rejected_with_Conflict_before_any_insert()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var generations = Substitute.For<ICacheGenerationCounter>();

        var existing = Exercise.CreateGlobal(
            "Barbell Bench Press", string.Empty, "desc",
            ExerciseType.Strength, MovementType.Compound, DifficultyLevel.Intermediate, Equipment.Barbell,
            null, null, new[] { (MuscleGroup.Chest, true) });
        // The duplicate-name pre-check (Query().AnyAsync) finds a row with the same DefaultName.
        repository.Query().Returns(new TestAsyncEnumerable<Exercise>(new[] { existing }));

        var sut = CreateSut(repository, unitOfWork, generations);

        var result = await sut.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        // Rejected before touching persistence or the cache.
        await repository.DidNotReceive().AddAsync(Arg.Any<Exercise>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await generations.DidNotReceive().IncrementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unparseable_exercise_type_is_rejected_with_Validation()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var generations = Substitute.For<ICacheGenerationCounter>();

        // No existing row → name pre-check passes, so enum parsing is reached.
        repository.Query().Returns(new TestAsyncEnumerable<Exercise>(Array.Empty<Exercise>()));

        var sut = CreateSut(repository, unitOfWork, generations);

        var command = ValidCommand() with { Type = "NotARealType" };
        var result = await sut.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);

        await repository.DidNotReceive().AddAsync(Arg.Any<Exercise>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_command_creates_exercise_saves_and_busts_search_cache()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var generations = Substitute.For<ICacheGenerationCounter>();

        repository.Query().Returns(new TestAsyncEnumerable<Exercise>(Array.Empty<Exercise>()));
        generations.IncrementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1L);

        var sut = CreateSut(repository, unitOfWork, generations);

        var result = await sut.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var newId = result.Value;
        Assert.NotEqual(Guid.Empty, newId);

        // Persisted: the aggregate built from the command, with the returned id and parsed enum fields.
        await repository.Received(1).AddAsync(
            Arg.Is<Exercise>(e =>
                e.Id == newId &&
                e.DefaultName == "Barbell Bench Press" &&
                e.Type == ExerciseType.Strength &&
                e.MovementType == MovementType.Compound &&
                e.Difficulty == DifficultyLevel.Intermediate &&
                e.Equipment == Equipment.Barbell),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // A new exercise invalidates every cached search page via the generation counter.
        await generations.Received(1).IncrementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
