namespace BuildingBlocks.Shared.Authorization;

public enum Permission
{
    // Plans
    PlanCreate,
    PlanUpdate,
    PlanDelete,
    PlanAssign,
    PlanView,

    // Clients
    ClientView,
    ClientRemove,
    InviteCreate,

    // Workout Logs
    WorkoutLogCreate,
    WorkoutLogViewOwn,
    WorkoutLogViewAll
}
