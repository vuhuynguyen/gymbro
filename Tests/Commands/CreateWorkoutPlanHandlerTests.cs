using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.DomainPrimitives;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands.Handlers;
using Modules.WorkoutPlanModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the plan-creation handler. It is a pure create: there is no business-rule branch that returns a
/// <c>Result</c> failure, so the contract under test is that the new <see cref="WorkoutPlan"/> is stamped
/// with the ambient tenant (from <see cref="ITenantContext"/>) and author (from <see cref="ICurrentUser"/>)
/// — never from the command — built from the command fields, persisted via the repository, committed with a
/// single SaveChanges, and that the returned id matches the created aggregate. The remaining guard the
/// handler relies on is the domain invariant in <see cref="WorkoutPlan.Create"/>: a blank name throws a
/// <see cref="DomainException"/> before any persistence happens (validation that the MediatR
/// <c>ValidationBehavior</c> normally screens, defended again at the aggregate boundary). Fully mocked — no
/// database.
/// </summary>
public sealed class CreateWorkoutPlanHandlerTests
{
    private static CreateWorkoutPlanHandler CreateSut(
        IWorkoutPlanRepository repository,
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid currentUserId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        var currentUser = Substitute.For<ICurrentUser>();
        tenantContext.TenantId.Returns(tenantId);
        currentUser.UserId.Returns(currentUserId);

        return new CreateWorkoutPlanHandler(repository, unitOfWork, tenantContext, currentUser);
    }

    [Fact]
    public async Task Valid_request_creates_plan_with_ambient_tenant_and_author_then_saves_once()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(repository, unitOfWork, tenantId, currentUserId);

        var command = new CreateWorkoutPlanCommand(
            Name: "Strength Block",
            Description: "5x5 progression",
            DurationWeeks: 8,
            WorkoutsPerWeek: 4);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var newId = result.Value;
        Assert.NotEqual(Guid.Empty, newId);

        // The aggregate is stamped from the ambient context (not the command) and from the command fields,
        // then handed to the repository exactly once; the returned id is the created plan's id.
        await repository.Received(1).AddAsync(
            Arg.Is<WorkoutPlan>(p =>
                p.Id == newId &&
                p.TenantId == tenantId &&
                p.CreatedBy == currentUserId &&
                p.Name == "Strength Block" &&
                p.Description == "5x5 progression" &&
                p.DurationWeeks == 8 &&
                p.WorkoutsPerWeek == 4 &&
                p.Version == 1 &&
                !p.IsDeleted),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Optional_fields_omitted_are_persisted_as_null()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = CreateSut(repository, unitOfWork, tenantId, currentUserId);

        var command = new CreateWorkoutPlanCommand(
            Name: "Minimal Plan",
            Description: null,
            DurationWeeks: null,
            WorkoutsPerWeek: null);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        await repository.Received(1).AddAsync(
            Arg.Is<WorkoutPlan>(p =>
                p.Name == "Minimal Plan" &&
                p.Description == null &&
                p.DurationWeeks == null &&
                p.WorkoutsPerWeek == null),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blank_name_is_rejected_by_the_aggregate_invariant_before_any_persistence()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var repository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var sut = CreateSut(repository, unitOfWork, tenantId, currentUserId);

        var command = new CreateWorkoutPlanCommand(
            Name: "   ",
            Description: null,
            DurationWeeks: null,
            WorkoutsPerWeek: null);

        // The factory the handler calls enforces "Name is required" at the aggregate boundary; the handler
        // does not swallow it, so it surfaces before AddAsync/SaveChanges run.
        await Assert.ThrowsAsync<DomainException>(
            () => sut.Handle(command, CancellationToken.None));

        await repository.DidNotReceive().AddAsync(Arg.Any<WorkoutPlan>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
