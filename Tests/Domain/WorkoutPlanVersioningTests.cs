using Modules.WorkoutPlanModule.Entities;
using Modules.WorkoutPlanModule.Application;
using BuildingBlocks.Shared.DomainPrimitives;
using Xunit;

namespace Gymbro.Tests.Domain;

public sealed class WorkoutPlanVersioningTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static WorkoutPlan CreatePlan(string name = "Test Plan") =>
        WorkoutPlan.Create(TenantId, CreatedBy, name, null, null, null);

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public void Create_starts_at_version_1()
    {
        var plan = CreatePlan();
        Assert.Equal(1, plan.Version);
    }

    [Fact]
    public void Create_assigns_unique_TemplateId()
    {
        var a = CreatePlan();
        var b = CreatePlan();
        Assert.NotEqual(a.TemplateId, b.TemplateId);
    }

    [Fact]
    public void Create_throws_when_name_is_empty()
    {
        Assert.Throws<DomainException>(() =>
            WorkoutPlan.Create(TenantId, CreatedBy, "", null, null, null));
    }

    [Fact]
    public void Create_throws_when_tenantId_is_empty()
    {
        Assert.Throws<DomainException>(() =>
            WorkoutPlan.Create(Guid.Empty, CreatedBy, "Plan", null, null, null));
    }

    [Fact]
    public void Create_throws_when_createdBy_is_empty()
    {
        Assert.Throws<DomainException>(() =>
            WorkoutPlan.Create(TenantId, Guid.Empty, "Plan", null, null, null));
    }

    // ── CreateNewVersion ───────────────────────────────────────────────────

    [Fact]
    public void CreateNewVersion_increments_version()
    {
        var v1 = CreatePlan();
        var v2 = WorkoutPlan.CreateNewVersion(v1, CreatedBy, "Plan v2", null, null, null);

        Assert.Equal(2, v2.Version);
    }

    [Fact]
    public void CreateNewVersion_preserves_TemplateId()
    {
        var v1 = CreatePlan();
        var v2 = WorkoutPlan.CreateNewVersion(v1, CreatedBy, "Plan v2", null, null, null);

        Assert.Equal(v1.TemplateId, v2.TemplateId);
    }

    [Fact]
    public void CreateNewVersion_creates_distinct_Id()
    {
        var v1 = CreatePlan();
        var v2 = WorkoutPlan.CreateNewVersion(v1, CreatedBy, "Plan v2", null, null, null);

        Assert.NotEqual(v1.Id, v2.Id);
    }

    [Fact]
    public void CreateNewVersion_preserves_TenantId()
    {
        var v1 = CreatePlan();
        var v2 = WorkoutPlan.CreateNewVersion(v1, CreatedBy, "Plan v2", null, null, null);

        Assert.Equal(v1.TenantId, v2.TenantId);
    }

    [Fact]
    public void CreateNewVersion_deep_copies_workouts()
    {
        var v1 = CreatePlan();
        v1.ReplaceStructure(new[]
        {
            ("Push Day", 1, (IReadOnlyList<(Guid, int, IReadOnlyList<PlanWorkoutSetData>, Guid?)>)new[]
            {
                (Guid.NewGuid(), 1, (IReadOnlyList<PlanWorkoutSetData>)new[]
                {
                    new PlanWorkoutSetData(PlanSetType.Working, 8, 100m, null, null, 90, 1)
                }, (Guid?)null)
            })
        });

        var v2 = WorkoutPlan.CreateNewVersion(v1, CreatedBy, "Plan v2", null, null, null);

        Assert.Single(v2.Workouts);
        Assert.Equal("Push Day", v2.Workouts.First().Name);
    }

    [Fact]
    public void CreateNewVersion_copied_workouts_have_different_Ids()
    {
        var v1 = CreatePlan();
        v1.ReplaceStructure(new[]
        {
            ("Push Day", 1, (IReadOnlyList<(Guid, int, IReadOnlyList<PlanWorkoutSetData>, Guid?)>)Array.Empty<(Guid, int, IReadOnlyList<PlanWorkoutSetData>, Guid?)>())
        });

        var v2 = WorkoutPlan.CreateNewVersion(v1, CreatedBy, "Plan v2", null, null, null);

        Assert.NotEqual(
            v1.Workouts.First().Id,
            v2.Workouts.First().Id);
    }

    [Fact]
    public void Chained_versioning_increments_monotonically()
    {
        var v1 = CreatePlan();
        var v2 = WorkoutPlan.CreateNewVersion(v1, CreatedBy, "v2", null, null, null);
        var v3 = WorkoutPlan.CreateNewVersion(v2, CreatedBy, "v3", null, null, null);

        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);
        Assert.Equal(3, v3.Version);
        Assert.Equal(v1.TemplateId, v3.TemplateId);
    }

    // ── Archive / Delete ───────────────────────────────────────────────────

    [Fact]
    public void SetArchived_toggles_IsArchived()
    {
        var plan = CreatePlan();

        plan.SetArchived(true);
        Assert.True(plan.IsArchived);

        plan.SetArchived(false);
        Assert.False(plan.IsArchived);
    }

    [Fact]
    public void MarkDeleted_sets_IsDeleted()
    {
        var plan = CreatePlan();
        plan.MarkDeleted();
        Assert.True(plan.IsDeleted);
    }
}
