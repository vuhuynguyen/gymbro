using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
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
/// Pins the platform-admin catalog-update guards: a missing exercise is rejected with the literal
/// <c>NotFound</c> code, a name already used by another live exercise is a <c>Conflict</c>, an unparseable
/// enum value is a <c>Validation</c> failure, and the happy path mutates the aggregate in place (catalog
/// fields + replaced child collections) and commits via a single SaveChanges. Fully mocked — no database.
/// The name-collision pre-check (<c>Query().AnyAsync(...)</c>) is backed by an in-memory async queryable.
/// </summary>
public sealed class UpdateExerciseHandlerTests
{
    private static UpdateExerciseHandler CreateSut(
        IExerciseRepository repository,
        IUnitOfWork unitOfWork)
    {
        // ExerciseCatalogCache is a sealed concrete dependency; build a real instance from substituted
        // collaborators. Invalidation runs only on the success path (best-effort, post-commit), so the
        // default no-op substitutes keep it inert.
        var distributedCache = Substitute.For<IDistributedCache>();
        var keyNamespace = Substitute.For<ICacheKeyNamespace>();
        var generations = Substitute.For<ICacheGenerationCounter>();
        var searchReader = new ExerciseCatalogSearchReader(Substitute.For<IExerciseRepository>());
        var detailReader = new ExerciseCatalogDetailReader(Substitute.For<IExerciseRepository>());
        var catalogCache = new ExerciseCatalogCache(
            distributedCache, keyNamespace, generations, searchReader, detailReader);

        return new UpdateExerciseHandler(repository, unitOfWork, catalogCache);
    }

    private static Exercise CreateExercise(string name)
        => Exercise.CreateGlobal(
            name,
            imageUrl: "",
            description: "old description",
            type: ExerciseType.Strength,
            movementType: MovementType.Compound,
            difficulty: DifficultyLevel.Beginner,
            equipment: Equipment.Barbell,
            estimatedCaloriesBurn: 50,
            averageDurationSeconds: 60,
            muscles: new[] { (MuscleGroup.Chest, true) });

    private static UpdateExerciseCommand CreateCommand(
        Guid exerciseId,
        string name = "Bench Press",
        string type = "Strength",
        string movementType = "Compound",
        string difficulty = "Beginner",
        string equipment = "Barbell")
        => new(
            exerciseId,
            Name: name,
            Description: "new description",
            Type: type,
            MovementType: movementType,
            Difficulty: difficulty,
            Equipment: equipment,
            EstimatedCaloriesBurn: 80,
            AverageDurationSeconds: 90,
            ImageUrl: "https://example/img.png",
            Muscles: new[] { new ExerciseMuscleInput("Chest", true) },
            Instructions: new[] { "Lower the bar", "Press up" },
            Tags: new[] { "compound" },
            Media: new[] { new ExerciseMediaInput("https://example/clip.mp4", "Video") },
            Warnings: new[] { "Keep wrists straight" });

    [Fact]
    public async Task Missing_exercise_returns_not_found_and_never_persists()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        repository.GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Exercise?)null);

        var sut = CreateSut(repository, unitOfWork);

        var result = await sut.Handle(CreateCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Name_used_by_another_exercise_returns_conflict_and_never_persists()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var target = CreateExercise("Squat");
        repository.GetForUpdateAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        // A different, live exercise already owns the requested name → AnyAsync pre-check is true.
        var other = CreateExercise("Bench Press");
        repository.Query()
            .Returns(new TestAsyncEnumerable<Exercise>(new[] { other }));

        var sut = CreateSut(repository, unitOfWork);

        var result = await sut.Handle(
            CreateCommand(target.Id, name: "Bench Press"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_enum_value_returns_validation_and_never_persists()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var target = CreateExercise("Bench Press");
        repository.GetForUpdateAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        // Name unchanged and no other holder → collision pre-check passes; the type string is unparseable.
        repository.Query()
            .Returns(new TestAsyncEnumerable<Exercise>(System.Array.Empty<Exercise>()));

        var sut = CreateSut(repository, unitOfWork);

        var result = await sut.Handle(
            CreateCommand(target.Id, name: "Bench Press", type: "NotARealType"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_update_mutates_aggregate_and_saves_once()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var target = CreateExercise("Bench Press");
        repository.GetForUpdateAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        // No other exercise holds the name → collision pre-check passes.
        repository.Query()
            .Returns(new TestAsyncEnumerable<Exercise>(System.Array.Empty<Exercise>()));

        var sut = CreateSut(repository, unitOfWork);

        var result = await sut.Handle(CreateCommand(target.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value!;
        Assert.Equal(target.Id, value);

        // Catalog fields updated in place on the tracked aggregate.
        Assert.Equal("Bench Press", target.DefaultName);
        Assert.Equal("new description", target.DefaultDescription);
        Assert.Equal("https://example/img.png", target.ImageUrl);
        Assert.Equal(80, target.EstimatedCaloriesBurn);
        Assert.Equal(90, target.AverageDurationSeconds);

        // Child collections replaced from the command.
        Assert.Single(target.Muscles);
        Assert.Equal(2, target.Instructions.Count);
        Assert.Single(target.Tags);
        Assert.Single(target.Media);
        Assert.Single(target.Warnings);

        // Committed exactly once.
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
