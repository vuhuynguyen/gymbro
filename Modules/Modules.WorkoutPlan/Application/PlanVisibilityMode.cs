namespace Modules.WorkoutPlanModule.Application;

/// <summary>
/// Plan-assignment visibility mode. Part of the WorkoutPlan module's <b>public contract</b> — it is
/// consumed cross-module by WorkoutSession at session start — so it lives in the Application/contracts
/// namespace, not <c>Entities</c>. Other modules reference this contract type, never the domain entities
/// (boundary rule: no module references another module's <c>.Entities</c>). Persisted as <c>int</c>
/// (see <c>PlanAssignmentConfiguration</c> <c>HasConversion&lt;int&gt;</c>); values are load-bearing — do not renumber.
/// </summary>
public enum PlanVisibilityMode
{
    Full = 1,
    Guided = 2,
    Blind = 3
}
