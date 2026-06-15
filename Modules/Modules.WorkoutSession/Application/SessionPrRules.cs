using BuildingBlocks.Shared.Tracking;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application;

/// <summary>
/// Single source of truth for "which logged set is eligible to set an estimated-1RM personal record".
/// Without this gate a bogus PR appears for high-rep sets (Epley e1RM is computed for any positive reps,
/// so a 20-rep set yields an inflated estimate) or for non-strength lifts. Used by the session list
/// (<c>CompleteSessionHandler.PrCount</c>), the detail view (<c>SessionMapping.DetectPrs</c>) and the
/// Progress overview so all three agree — see AUDIT-2026-06-14 finding 4 and BUSINESS_RULES.md "PR count".
/// </summary>
public static class SessionPrRules
{
    /// <summary>Max reps for a credible e1RM estimate — Epley inflates badly past this.</summary>
    public const int MaxPrReps = 12;

    /// <summary>Only strength/bodyweight lifts carry a meaningful estimated 1RM.</summary>
    public static bool IsPrEligibleLift(ExerciseTrackingType trackingType) =>
        trackingType is ExerciseTrackingType.Strength or ExerciseTrackingType.Bodyweight;

    /// <summary>
    /// True when this performed set may set an e1RM PR: a working set on a strength/bodyweight lift with a
    /// computed e1RM and reps within the credible window. A non-null e1RM already implies positive weight
    /// and reps (it is null otherwise), so this is the complete eligibility predicate.
    /// </summary>
    public static bool IsPrEligibleSet(ExerciseTrackingType trackingType, PerformedSet set) =>
        IsPrEligibleLift(trackingType)
        && set.SetType == PerformedSetType.Working
        && set.EstimatedOneRepMaxKg is not null
        && set.Reps is > 0 and <= MaxPrReps;
}
