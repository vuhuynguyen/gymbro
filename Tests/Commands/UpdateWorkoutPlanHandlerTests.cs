using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands.Handlers;
using Modules.WorkoutPlanModule.Entities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the plan-edit guards. An edit (a) must target an existing plan (missing → NotFound), (b) is rejected
/// while the plan is archived (Conflict), (c) may only be performed by the plan's author or an admin
/// (non-author → Forbidden), and (d) on success appends a new immutable version via the repository and a
/// single SaveChanges; a concurrent same-version insert surfaced as a <see cref="DbUpdateException"/> is mapped
/// to a clean Conflict. Fully mocked — no database.
/// </summary>
public sealed class UpdateWorkoutPlanHandlerTests
{
    private static UpdateWorkoutPlanHandler CreateSut(
        IWorkoutPlanRepository repository,
        IUnitOfWork unitOfWork,
        Guid currentUserId,
        bool isAdmin = false)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(currentUserId);
        currentUser.IsAdmin.Returns(isAdmin);

        return new UpdateWorkoutPlanHandler(repository, unitOfWork, currentUser);
    }

    private static WorkoutPlan CreatePlan(Guid tenantId, Guid author)
        => WorkoutPlan.Create(tenantId, author, "Strength Block", "desc", 8, 4);

    private static UpdateWorkoutPlanCommand CreateCommand(Guid planId)
        => new(planId, "Strength Block v2", "updated", 10, 5);

    [Fact]
    public async Task Missing_plan_returns_not_found_and_never_persists()
    {
        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        repository.GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkoutPlan?)null);

        var sut = CreateSut(repository, unitOfWork, Guid.NewGuid());

        var result = await sut.Handle(CreateCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);

        await repository.DidNotReceive().AddAsync(Arg.Any<WorkoutPlan>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Editing_an_archived_plan_returns_conflict()
    {
        var tenantId = Guid.NewGuid();
        var author = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = CreatePlan(tenantId, author);
        plan.SetArchived(true);
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var sut = CreateSut(repository, unitOfWork, author);

        var result = await sut.Handle(CreateCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        await repository.DidNotReceive().AddAsync(Arg.Any<WorkoutPlan>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_author_non_admin_caller_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var author = Guid.NewGuid();
        var caller = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = CreatePlan(tenantId, author);
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        // The plan being edited is already the latest version in its template.
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>())
            .Returns(plan);

        var sut = CreateSut(repository, unitOfWork, caller);

        var result = await sut.Handle(CreateCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);

        await repository.DidNotReceive().AddAsync(Arg.Any<WorkoutPlan>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Author_editing_draft_head_replaces_it_at_the_same_version()
    {
        var tenantId = Guid.NewGuid();
        var author = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // A freshly created plan is a draft head; editing it must NOT bump the version (no version inflation).
        var plan = CreatePlan(tenantId, author);
        Assert.True(plan.IsDraft);
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>())
            .Returns(plan);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(repository, unitOfWork, author);

        var result = await sut.Handle(CreateCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The draft is replaced in place: a new draft row at the SAME version, the old draft dropped, one commit.
        await repository.Received(1).AddAsync(
            Arg.Is<WorkoutPlan>(p =>
                p.TemplateId == plan.TemplateId &&
                p.Version == plan.Version &&
                p.IsDraft &&
                p.Id != plan.Id &&
                p.Name == "Strength Block v2" &&
                p.CreatedBy == author),
            Arg.Any<CancellationToken>());
        repository.Received(1).Remove(plan);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Author_editing_published_head_forks_a_new_draft_version()
    {
        var tenantId = Guid.NewGuid();
        var author = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        // The head is already published → the first edit forks a NEW draft one version above it.
        var plan = CreatePlan(tenantId, author);
        plan.Publish();
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>())
            .Returns(plan);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(repository, unitOfWork, author);

        var result = await sut.Handle(CreateCommand(plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await repository.Received(1).AddAsync(
            Arg.Is<WorkoutPlan>(p =>
                p.TemplateId == plan.TemplateId &&
                p.Version == plan.Version + 1 &&
                p.IsDraft &&
                p.Id != plan.Id &&
                p.Name == "Strength Block v2"),
            Arg.Any<CancellationToken>());
        // A published version is immutable — it must NOT be removed when forking a draft off it.
        repository.DidNotReceive().Remove(plan);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_same_version_insert_maps_db_update_exception_to_conflict()
    {
        var tenantId = Guid.NewGuid();
        var author = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var plan = CreatePlan(tenantId, author);
        repository.GetForUpdateAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.GetLatestVersionInTemplateAsync(plan.TemplateId, Arg.Any<CancellationToken>())
            .Returns(plan);
        // A concurrent edit already created the same version → unique-index violation at SaveChanges.
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException());

        var sut = CreateSut(repository, unitOfWork, author);

        var result = await sut.Handle(CreateCommand(plan.Id), CancellationToken.None);

        // Mapped to Conflict (409) rather than bubbling up as an unhandled 500.
        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }
}
