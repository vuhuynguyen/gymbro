using BuildingBlocks.Shared.Tracking;

namespace Modules.ExerciseModule.Entities;

/// <summary>
/// Derives a sensible default <see cref="ExerciseTrackingType"/> from the descriptive
/// <see cref="ExerciseType"/>/<see cref="Equipment"/> of an exercise, used when a caller (or legacy data) does not
/// specify a tracking type. The data-migration backfill mirrors this mapping in SQL.
/// </summary>
public static class ExerciseTrackingDefaults
{
    public static ExerciseTrackingType Derive(ExerciseType type, Equipment equipment) => type switch
    {
        ExerciseType.Cardio => ExerciseTrackingType.Cardio,
        ExerciseType.Mobility or ExerciseType.Stretching => ExerciseTrackingType.Mobility,
        _ => equipment == Equipment.Bodyweight ? ExerciseTrackingType.Bodyweight : ExerciseTrackingType.Strength
    };
}
