using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands.Handlers;
using Modules.WorkoutPlanModule.Entities;
using Modules.WorkoutPlanModule.Application;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the structure-replace guards for the plan builder. Editing forks a new plan version, so the handler:
/// validates the referenced exercises (cross-module via <see cref="ValidateExerciseIdsQuery"/>), rejects a
/// missing plan (NotFound) and an archived plan (Conflict) before doing any version work, enforces author
/// ownership (Forbidden) via <c>PlanAuthorPolicy</c>, maps a concurrent-version unique-index violation at
/// SaveChanges to a clean Conflict, and on the happy path creates the next version, replaces its structure,
/// adds it through the repository and commits exactly once. Fully mocked — no database.
/// </summary>
public sealed class ReplaceWorkoutPlanStructureHandlerTests
{
    private static ReplaceWorkoutPlanStructureHandler CreateSut(
        IWorkoutPlanRepository repository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        Guid currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);

        return new ReplaceWorkoutPlanStructureHandler(repository, mediator, unitOfWork, currentUser);
    }

    private static WorkoutPlan CreatePlan(Guid tenantId, Guid author)
        => WorkoutPlan.Create(tenantId, author, "Strength Block", null, null, null);

    private static ReplaceWorkoutPlanStructureCommand CreateCommand(Guid planId, Guid exerciseId)
        => new(
            planId,
            "Strength Block",
            Description: null,
            DurationWeeks: null,
            WorkoutsPerWeek: null,
            new[]
            {
                new PlanWorkoutStructureInput(
                    "Day A",
                    Order: 1,
                    new[]
                    {
                        new PlanWorkoutExerciseInput(
                            exerciseId,
                            Order: 1,
                            new[]
                            {
                                new PlanSetInput(
                                    PlanSetType.Working,
                                    TargetReps: 5,
                                    TargetWeightKg: 100m,
                                    TargetRpe: 8,
                                    TargetDurationSeconds: null,
                                    RestSeconds: 120,
                                    Order: 1)
                            })
                    })
            });

    [Fact]
    public async Task Invalid_exercise_ids_short_circuit_with_the_validation_failure_before_any_version_work()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var mediator = Substitute.For<IMediator>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // The cross-module exercise validation fails; the handler returns that exact failure verbatim.
        mediator.Send(Arg.Any<ValidateExerciseIdsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(BuildingBlocks.Shared.Errors.Error.Validation("Unknown exercise.")));

        var sut = CreateSut(repository, mediator, unitOfWork, authorId);

        var result = await sut.Handle(CreateCommand(planId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Code);

        // Rejected before loading or persisting anything.
        await repository.DidNotReceive().GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<WorkoutPlan>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_plan_returns_not_found_and_never_persists()
    {
        var authorId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var mediator = Substitute.For<IMediator>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        mediator.Send(Arg.Any<ValidateExerciseIdsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        repository.GetForUpdateAsync(planId, Arg.Any<CancellationToken>())
            .Returns((WorkoutPlan?)null);

        var sut = CreateSut(repository, mediator, unitOfWork, authorId);

        var result = await sut.Handle(CreateCommand(planId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await repository.DidNotReceive().AddAsync(Arg.Any<WorkoutPlan>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_author_is_forbidden_from_replacing_the_structure()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var mediator = Substitute.For<IMediator>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // The plan is authored by someone else; the caller is not an admin.
        var plan = CreatePlan(tenantId, authorId);
        mediator.Send(Arg.Any<ValidateExerciseIdsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);

        var sut = CreateSut(repository, mediator, unitOfWork, callerId);

        var result = await sut.Handle(CreateCommand(plan.Id, exerciseId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);

        await repository.DidNotReceive().AddAsync(Arg.Any<WorkoutPlan>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Author_editing_draft_head_replaces_it_at_same_version_and_commits_once()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var mediator = Substitute.For<IMediator>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // A freshly created plan is a draft head; the builder save replaces it in place (no version bump).
        var plan = CreatePlan(tenantId, authorId);
        Assert.True(plan.IsDraft);
        mediator.Send(Arg.Any<ValidateExerciseIdsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        // current == latest (same row) so the "not the latest version" guard passes.
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(repository, mediator, unitOfWork, authorId);

        var result = await sut.Handle(CreateCommand(plan.Id, exerciseId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The new draft head's id is returned (not the stale one) so the caller can re-point to the latest.
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.NotEqual(plan.Id, result.Value);

        // The draft is replaced at the SAME version (no inflation), same template/author, carrying the new
        // structure; the old draft is dropped and the unit of work commits exactly once.
        await repository.Received(1).AddAsync(
            Arg.Is<WorkoutPlan>(p =>
                p.Id != plan.Id &&
                p.TemplateId == plan.TemplateId &&
                p.Version == plan.Version &&
                p.IsDraft &&
                p.CreatedBy == authorId &&
                p.Workouts.Count == 1),
            Arg.Any<CancellationToken>());
        repository.Received(1).Remove(plan);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Author_editing_published_head_forks_a_new_draft_version()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var mediator = Substitute.For<IMediator>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // The head is published → the builder save forks a new draft one version above it.
        var plan = CreatePlan(tenantId, authorId);
        plan.Publish();
        mediator.Send(Arg.Any<ValidateExerciseIdsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(repository, mediator, unitOfWork, authorId);

        var result = await sut.Handle(CreateCommand(plan.Id, exerciseId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await repository.Received(1).AddAsync(
            Arg.Is<WorkoutPlan>(p =>
                p.Id != plan.Id &&
                p.TemplateId == plan.TemplateId &&
                p.Version == plan.Version + 1 &&
                p.IsDraft &&
                p.Workouts.Count == 1),
            Arg.Any<CancellationToken>());
        repository.DidNotReceive().Remove(plan);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_version_violating_unique_index_maps_to_conflict()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var mediator = Substitute.For<IMediator>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = CreatePlan(tenantId, authorId);
        mediator.Send(Arg.Any<ValidateExerciseIdsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>()).Returns(plan);
        // A concurrent editor inserted the same next version first; the unique index rejects this commit.
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException());

        var sut = CreateSut(repository, mediator, unitOfWork, authorId);

        var result = await sut.Handle(CreateCommand(plan.Id, exerciseId), CancellationToken.None);

        // Mapped to Conflict (409) rather than bubbling up as an unhandled 500.
        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }
}
