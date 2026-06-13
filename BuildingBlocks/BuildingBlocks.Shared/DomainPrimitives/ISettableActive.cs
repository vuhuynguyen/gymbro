namespace BuildingBlocks.Shared.DomainPrimitives;

/// <summary>
/// A plan assignment whose active/paused state can be toggled. Lets the shared set-active handler logic be
/// written once for both modules (see <c>BuildingBlocks.Application.Plans.PlanAssignmentLifecycle</c>).
/// </summary>
public interface ISettableActive
{
    void SetActive(bool active);
}
