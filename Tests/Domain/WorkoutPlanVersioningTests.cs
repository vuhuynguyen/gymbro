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
    public void Create_starts_as_draft()
    {
        var plan = CreatePlan();
        Assert.True(plan.IsDraft);
    }

    [Fact]
    public void Publish_clears_draft_flag()
    {
        var plan = CreatePlan();
        plan.Publish();
        Assert.False(plan.IsDraft);
    }

    [Fact]
    public void Publish_throws_when_already_published()
    {
        var plan = CreatePlan();
        plan.Publish();
        Assert.Throws<DomainException>(() => plan.Publish());
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

    // ── CreateDraft ────────────────────────────────────────────────────────

    [Fact]
    public void CreateDraft_uses_the_caller_supplied_version()
    {
        var v1 = CreatePlan();
        var next = WorkoutPlan.CreateDraft(v1, CreatedBy, v1.Version + 1, "Plan v2", null, null, null);

        Assert.Equal(2, next.Version);
    }

    [Fact]
    public void CreateDraft_starts_as_draft()
    {
        var v1 = CreatePlan();
        var next = WorkoutPlan.CreateDraft(v1, CreatedBy, v1.Version + 1, "Plan v2", null, null, null);

        Assert.True(next.IsDraft);
    }

    [Fact]
    public void CreateDraft_preserves_TemplateId()
    {
        var v1 = CreatePlan();
        var next = WorkoutPlan.CreateDraft(v1, CreatedBy, v1.Version + 1, "Plan v2", null, null, null);

        Assert.Equal(v1.TemplateId, next.TemplateId);
    }

    [Fact]
    public void CreateDraft_creates_distinct_Id()
    {
        var v1 = CreatePlan();
        var next = WorkoutPlan.CreateDraft(v1, CreatedBy, v1.Version + 1, "Plan v2", null, null, null);

        Assert.NotEqual(v1.Id, next.Id);
    }

    [Fact]
    public void CreateDraft_preserves_TenantId()
    {
        var v1 = CreatePlan();
        var next = WorkoutPlan.CreateDraft(v1, CreatedBy, v1.Version + 1, "Plan v2", null, null, null);

        Assert.Equal(v1.TenantId, next.TenantId);
    }

    [Fact]
    public void CreateDraft_deep_copies_workouts()
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

        var next = WorkoutPlan.CreateDraft(v1, CreatedBy, v1.Version + 1, "Plan v2", null, null, null);

        Assert.Single(next.Workouts);
        Assert.Equal("Push Day", next.Workouts.First().Name);
    }

    [Fact]
    public void CreateDraft_copied_workouts_have_different_Ids()
    {
        var v1 = CreatePlan();
        v1.ReplaceStructure(new[]
        {
            ("Push Day", 1, (IReadOnlyList<(Guid, int, IReadOnlyList<PlanWorkoutSetData>, Guid?)>)Array.Empty<(Guid, int, IReadOnlyList<PlanWorkoutSetData>, Guid?)>())
        });

        var next = WorkoutPlan.CreateDraft(v1, CreatedBy, v1.Version + 1, "Plan v2", null, null, null);

        Assert.NotEqual(
            v1.Workouts.First().Id,
            next.Workouts.First().Id);
    }

    [Fact]
    public void Draft_replacement_keeps_same_version_then_publish_advances_it()
    {
        // Mirrors the handler flow: a brand-new plan is a draft v1; editing the draft replaces it at the
        // SAME version (no inflation); publishing makes v1 live; the next edit forks a draft v2.
        var v1Draft = CreatePlan();
        var v1Edited = WorkoutPlan.CreateDraft(v1Draft, CreatedBy, v1Draft.Version, "v1 edited", null, null, null);
        Assert.Equal(1, v1Edited.Version);
        Assert.True(v1Edited.IsDraft);

        v1Edited.Publish();
        Assert.False(v1Edited.IsDraft);

        var v2Draft = WorkoutPlan.CreateDraft(v1Edited, CreatedBy, v1Edited.Version + 1, "v2", null, null, null);
        Assert.Equal(2, v2Draft.Version);
        Assert.True(v2Draft.IsDraft);
        Assert.Equal(v1Draft.TemplateId, v2Draft.TemplateId);
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
