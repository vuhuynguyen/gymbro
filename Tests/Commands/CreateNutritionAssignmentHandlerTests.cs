using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the nutrition-assignment guards (mirrors CreatePlanAssignmentHandlerTests): member-only assignment,
/// no duplicate live assignment, and — the nutrition-specific part — that the plan is snapshotted to jsonb at
/// assign time so the daily log can seed from it. Fully mocked, no database.
/// </summary>
public sealed class CreateNutritionAssignmentHandlerTests
{
    private static NutritionPlan PlanWithMeal(Guid tenantId, Guid coachId, Guid foodId)
    {
        var plan = NutritionPlan.Create(tenantId, coachId, "Cut Plan", null);
        plan.ReplaceStructure(new[]
        {
            new PlanMealData("Breakfast", 1, new TimeOnly(8, 0), DayApplicability.EveryDay,
                new[] { new PlanMealItemData(foodId, 1, 1m, "Oats", "1 bowl", 300m, 10m, 50m, 6m, 8m) })
        });
        return plan;
    }

    private static CreateNutritionAssignmentHandler CreateSut(
        INutritionPlanAssignmentRepository assignmentRepository,
        INutritionPlanRepository planRepository,
        IUnitOfWork unitOfWork,
        ITenantRoleResolver roleResolver,
        Guid tenantId,
        Guid coachId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        var currentUser = Substitute.For<ICurrentUser>();
        tenantContext.TenantId.Returns(tenantId);
        currentUser.UserId.Returns(coachId);
        return new CreateNutritionAssignmentHandler(
            assignmentRepository, planRepository, unitOfWork, tenantContext, currentUser, roleResolver);
    }

    private static CreateNutritionAssignmentCommand Command(Guid traineeId, Guid planId)
        => new(traineeId, planId, new DateOnly(2026, 6, 1), null, NutritionVisibilityMode.Full, false, false);

    [Fact]
    public async Task Happy_path_snapshots_the_plan_and_persists_the_assignment()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var foodId = Guid.NewGuid();

        var planRepository = Substitute.For<INutritionPlanRepository>();
        planRepository.GetForUpdateAsync(planId, Arg.Any<CancellationToken>())
            .Returns(PlanWithMeal(tenantId, coachId, foodId));

        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        assignmentRepository.Query().Returns(new TestAsyncEnumerable<NutritionPlanAssignment>(Array.Empty<NutritionPlanAssignment>()));
        NutritionPlanAssignment? captured = null;
        assignmentRepository.AddAsync(Arg.Do<NutritionPlanAssignment>(a => captured = a), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var roleResolver = Substitute.For<ITenantRoleResolver>();
        roleResolver.GetRoleAsync(traineeId, tenantId, Arg.Any<CancellationToken>()).Returns(TenantRole.Client);
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var sut = CreateSut(assignmentRepository, planRepository, unitOfWork, roleResolver, tenantId, coachId);

        var result = await sut.Handle(Command(traineeId, planId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.PlanVersion);
        // The plan was snapshotted to jsonb (the day-log seeds from this).
        Assert.False(string.IsNullOrWhiteSpace(captured.SnapshotJson));
        Assert.Contains("Oats", captured.SnapshotJson);
    }

    [Fact]
    public async Task Non_member_trainee_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var planRepository = Substitute.For<INutritionPlanRepository>();
        planRepository.GetForUpdateAsync(planId, Arg.Any<CancellationToken>())
            .Returns(PlanWithMeal(tenantId, coachId, Guid.NewGuid()));
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        var roleResolver = Substitute.For<ITenantRoleResolver>();
        roleResolver.GetRoleAsync(Arg.Any<Guid>(), tenantId, Arg.Any<CancellationToken>()).Returns((TenantRole?)null);

        var sut = CreateSut(assignmentRepository, planRepository, Substitute.For<IUnitOfWork>(), roleResolver, tenantId, coachId);

        var result = await sut.Handle(Command(Guid.NewGuid(), planId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NutritionAssignment.TraineeNotMember", result.Error.Code);
    }

    [Fact]
    public async Task Duplicate_live_assignment_is_rejected_with_conflict()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var planRepository = Substitute.For<INutritionPlanRepository>();
        planRepository.GetForUpdateAsync(planId, Arg.Any<CancellationToken>())
            .Returns(PlanWithMeal(tenantId, coachId, Guid.NewGuid()));

        var existing = NutritionPlanAssignment.Create(
            tenantId, coachId, traineeId, planId, 1, new DateOnly(2026, 5, 1), null,
            NutritionVisibilityMode.Full, false, false, "{}");
        var assignmentRepository = Substitute.For<INutritionPlanAssignmentRepository>();
        assignmentRepository.Query().Returns(new TestAsyncEnumerable<NutritionPlanAssignment>(new[] { existing }));

        var roleResolver = Substitute.For<ITenantRoleResolver>();
        roleResolver.GetRoleAsync(traineeId, tenantId, Arg.Any<CancellationToken>()).Returns(TenantRole.Client);

        var sut = CreateSut(assignmentRepository, planRepository, Substitute.For<IUnitOfWork>(), roleResolver, tenantId, coachId);

        var result = await sut.Handle(Command(traineeId, planId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }
}
