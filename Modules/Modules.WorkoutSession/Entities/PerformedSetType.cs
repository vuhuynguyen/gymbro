namespace Modules.WorkoutSessionModule.Entities;

public enum PerformedSetType
{
    Warmup = 1,
    Working = 2,
    Drop = 3,
    Amrap = 4,
    Failure = 5,
    // Cluster / rest-pause: one logical set broken into mini-sets with short intra-set rest (e.g. 6 → 4 → 3 → 2
    // with ~15s between); the load may hold or drop across mini-sets — each stage carries its own weight/reps.
    // Like Drop, a Cluster stage links to its lead via ParentSetId and counts as ONE working set; its reps count
    // toward volume (volume = non-warmup sets). Appended ⇒ no migration.
    Cluster = 6
}
