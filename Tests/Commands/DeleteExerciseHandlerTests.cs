using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Application.Commands.Handlers;
using Modules.ExerciseModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the platform-admin exercise delete: a missing exercise short-circuits to the literal
/// <c>NotFound</c> code before any persistence work, and the happy path soft-deletes via the repository,
/// commits exactly once, and then busts the catalog cache (detail key eviction + search generation bump).
/// The repository and unit of work are mocked (no database); the cache is a real
/// <see cref="ExerciseCatalogCache"/> over an in-memory distributed cache and a substituted generation
/// counter so the invalidation side effect can be observed.
/// </summary>
public sealed class DeleteExerciseHandlerTests
{
    private static DeleteExerciseHandler CreateSut(
        IExerciseRepository repository,
        IUnitOfWork unitOfWork,
        ICacheGenerationCounter generations)
    {
        var catalogCache = new ExerciseCatalogCache(
            new MemoryDistributedCache(
                new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions())),
            CacheKeyNamespace.FromEnvironment("test"),
            generations,
            new ExerciseCatalogSearchReader(repository),
            new ExerciseCatalogDetailReader(repository));

        return new DeleteExerciseHandler(repository, unitOfWork, catalogCache);
    }

    private static Exercise CreateExercise()
        => Exercise.CreateGlobal(
            "Back Squat",
            imageUrl: "",
            description: "A compound lower-body lift.",
            ExerciseType.Strength,
            MovementType.Compound,
            DifficultyLevel.Intermediate,
            Equipment.Barbell,
            estimatedCaloriesBurn: null,
            averageDurationSeconds: null,
            muscles: new[] { (MuscleGroup.Legs, true) });

    [Fact]
    public async Task Missing_exercise_returns_not_found_and_never_persists_or_invalidates()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var generations = Substitute.For<ICacheGenerationCounter>();

        var exerciseId = Guid.NewGuid();
        repository.GetForUpdateAsync(exerciseId, Arg.Any<CancellationToken>())
            .Returns((Exercise?)null);

        var sut = CreateSut(repository, unitOfWork, generations);

        var result = await sut.Handle(new DeleteExerciseCommand(exerciseId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        // Rejected before any persistence or cache work.
        repository.DidNotReceive().Remove(Arg.Any<Exercise>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await generations.DidNotReceive().IncrementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_exercise_is_removed_committed_and_cache_invalidated()
    {
        var repository = Substitute.For<IExerciseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var generations = Substitute.For<ICacheGenerationCounter>();

        var exercise = CreateExercise();
        repository.GetForUpdateAsync(exercise.Id, Arg.Any<CancellationToken>())
            .Returns(exercise);

        var sut = CreateSut(repository, unitOfWork, generations);

        var result = await sut.Handle(new DeleteExerciseCommand(exercise.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Soft-delete staged on the loaded aggregate, then committed exactly once.
        repository.Received(1).Remove(exercise);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Search cache busted by bumping the generation counter (the detail key is evicted separately
        // against the in-memory cache).
        await generations.Received(1).IncrementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}