namespace Modules.WorkoutPlanModule.Application;

/// <summary>
/// Prescribed set type for a planned exercise. Lives in the <c>Application</c> namespace (not <c>Entities</c>)
/// because it is part of the cross-module contract surfaced by <c>PlanWorkoutDetailDto</c> /
/// <c>GetWorkoutForSnapshotQuery</c> that the WorkoutSession module consumes — exposing it from
/// <c>Entities</c> would leak the domain namespace across the module boundary (see
/// <c>ModuleBoundaryConventionTests</c>). Mirrors <c>PlanVisibilityMode</c>.
/// </summary>
public enum PlanSetType
{
    Warmup = 1,
    Working = 2,
    Drop = 3,
    Amrap = 4
}
