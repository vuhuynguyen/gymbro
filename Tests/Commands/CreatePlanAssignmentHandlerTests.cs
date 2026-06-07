using System.Linq.Expressions;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using Microsoft.EntityFrameworkCore.Query;
using Modules.WorkoutPlanModule.Application;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Commands;
using Modules.WorkoutPlanModule.Application.Commands.Handlers;
using Modules.WorkoutPlanModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the assignment-creation guards: a plan may only be assigned to a tenant member (non-members are
/// rejected with the literal <c>PlanAssignment.TraineeNotMember</c> code), the same plan cannot be
/// live-assigned to the same trainee twice (Conflict), and the happy path persists the assignment via the
/// repository plus a single SaveChanges. Fully mocked — no database.
/// </summary>
public sealed class CreatePlanAssignmentHandlerTests
{
    private static CreatePlanAssignmentHandler CreateSut(
        IPlanAssignmentRepository assignmentRepository,
        IWorkoutPlanRepository workoutPlanRepository,
        IUnitOfWork unitOfWork,
        ITenantRoleResolver roleResolver,
        Guid tenantId,
        Guid currentUserId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        var currentUser = Substitute.For<ICurrentUser>();
        tenantContext.TenantId.Returns(tenantId);
        currentUser.UserId.Returns(currentUserId);

        return new CreatePlanAssignmentHandler(
            assignmentRepository,
            workoutPlanRepository,
            unitOfWork,
            tenantContext,
            currentUser,
            roleResolver);
    }

    private static WorkoutPlan CreatePlan(Guid tenantId, Guid createdBy)
        => WorkoutPlan.Create(tenantId, createdBy, "Strength Block", null, null, null);

    private static CreatePlanAssignmentCommand CreateCommand(Guid traineeId, Guid planId)
        => new(
            traineeId,
            planId,
            new DateOnly(2026, 6, 1),
            FrequencyDaysPerWeek: 3,
            VisibilityMode: PlanVisibilityMode.Full,
            HideExercises: false,
            HideSetsReps: false,
            HideFutureWorkouts: false,
            DisableTraineeEditing: false,
            SnapshotJson: null);

    [Fact]
    public async Task Trainee_that_is_not_a_tenant_member_is_rejected_with_TraineeNotMember()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var workoutPlanRepository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var roleResolver = Substitute.For<ITenantRoleResolver>();

        workoutPlanRepository.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(CreatePlan(tenantId, currentUserId));
        // The trainee has no role in this tenant → not a member.
        roleResolver.GetRoleAsync(traineeId, tenantId, Arg.Any<CancellationToken>())
            .Returns((TenantRole?)null);

        var sut = CreateSut(
            assignmentRepository, workoutPlanRepository, unitOfWork, roleResolver, tenantId, currentUserId);

        var result = await sut.Handle(CreateCommand(traineeId, planId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlanAssignment.TraineeNotMember", result.Error.Code);

        // Rejected before any persistence work.
        await assignmentRepository.DidNotReceive()
            .AddAsync(Arg.Any<PlanAssignment>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_live_assignment_of_same_plan_to_same_trainee_is_conflict()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var workoutPlanRepository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var roleResolver = Substitute.For<ITenantRoleResolver>();

        var plan = CreatePlan(tenantId, currentUserId);
        workoutPlanRepository.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns(plan);
        roleResolver.GetRoleAsync(traineeId, tenantId, Arg.Any<CancellationToken>())
            .Returns(TenantRole.Client);

        // An existing live assignment of this plan to this trainee makes the AnyAsync pre-check true.
        var existing = PlanAssignment.Create(
            tenantId, currentUserId, traineeId, planId, plan.Version, new DateOnly(2026, 5, 1),
            3, PlanVisibilityMode.Full, false, false, false, false, null);
        assignmentRepository.Query()
            .Returns(new TestAsyncEnumerable<PlanAssignment>(new[] { existing }));

        var sut = CreateSut(
            assignmentRepository, workoutPlanRepository, unitOfWork, roleResolver, tenantId, currentUserId);

        var result = await sut.Handle(CreateCommand(traineeId, planId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        await assignmentRepository.DidNotReceive()
            .AddAsync(Arg.Any<PlanAssignment>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_request_creates_assignment_via_repository_and_saves()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var assignmentRepository = Substitute.For<IPlanAssignmentRepository>();
        var workoutPlanRepository = Substitute.For<IWorkoutPlanRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var roleResolver = Substitute.For<ITenantRoleResolver>();

        var plan = CreatePlan(tenantId, currentUserId);
        workoutPlanRepository.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns(plan);
        roleResolver.GetRoleAsync(traineeId, tenantId, Arg.Any<CancellationToken>())
            .Returns(TenantRole.Client);
        // No existing assignment → duplicate pre-check passes.
        assignmentRepository.Query()
            .Returns(new TestAsyncEnumerable<PlanAssignment>(Array.Empty<PlanAssignment>()));

        var sut = CreateSut(
            assignmentRepository, workoutPlanRepository, unitOfWork, roleResolver, tenantId, currentUserId);

        var result = await sut.Handle(CreateCommand(traineeId, planId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        // Persisted: the assignment built from the command + plan version, then a single SaveChanges.
        await assignmentRepository.Received(1).AddAsync(
            Arg.Is<PlanAssignment>(a =>
                a.TraineeId == traineeId &&
                a.PlanId == planId &&
                a.PlanVersion == plan.Version &&
                a.IsActive &&
                a.Id == result.Value),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Minimal in-memory <see cref="IQueryable{T}"/> that also supports EF Core's async operators
/// (e.g. <c>AnyAsync</c>) without a database, so the handler's <c>Query().AnyAsync(...)</c> pre-check
/// can be exercised against a plain list.
/// </summary>
internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }

    public TestAsyncEnumerable(Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = ((IQueryProvider)this).Execute(expression)!;
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, [executionResult])!;
    }
}

internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());
}
