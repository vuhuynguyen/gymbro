using System.Linq.Expressions;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Mapping;

internal static class ExerciseMapping
{
    public static Expression<Func<Exercise, ExerciseDto>> ExerciseDtoProjection =>
        x => new ExerciseDto
        {
            Id = x.Id,
            Name = x.DefaultName,
            Type = x.Type.ToString(),
            TrackingType = x.TrackingType.ToString(),
            MovementType = x.MovementType.ToString(),
            Difficulty = x.Difficulty.ToString(),
            Equipment = x.Equipment.ToString(),
            EstimatedCaloriesBurn = x.EstimatedCaloriesBurn,
            AverageDurationSeconds = x.AverageDurationSeconds,
            MuscleGroup = x.Muscles
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.Muscle)
                .Select(m => m.Muscle.ToString())
                .FirstOrDefault() ?? string.Empty,
            Category = x.Category,
            ImageUrl = x.ImageUrl,
            Muscles = x.Muscles
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.Muscle)
                .Select(m => new ExerciseMuscleItemDto
                {
                    Muscle = m.Muscle.ToString(),
                    IsPrimary = m.IsPrimary
                })
                .ToList()
        };

    public static Expression<Func<Exercise, ExerciseDetailDto>> ExerciseDetailProjection =>
        x => new ExerciseDetailDto
        {
            Id = x.Id,
            Name = x.DefaultName,
            Description = x.DefaultDescription,
            Type = x.Type.ToString(),
            TrackingType = x.TrackingType.ToString(),
            MovementType = x.MovementType.ToString(),
            Difficulty = x.Difficulty.ToString(),
            Equipment = x.Equipment.ToString(),
            EstimatedCaloriesBurn = x.EstimatedCaloriesBurn,
            AverageDurationSeconds = x.AverageDurationSeconds,
            MuscleGroup = x.Muscles
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.Muscle)
                .Select(m => m.Muscle.ToString())
                .FirstOrDefault() ?? string.Empty,
            Category = x.Category,
            ImageUrl = x.ImageUrl,
            Muscles = x.Muscles
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.Muscle)
                .Select(m => new ExerciseMuscleItemDto
                {
                    Muscle = m.Muscle.ToString(),
                    IsPrimary = m.IsPrimary
                })
                .ToList(),
            DetailedPrimaryMuscles = x.DetailedPrimaryMuscles,
            DetailedSecondaryMuscles = x.DetailedSecondaryMuscles,
            Instructions = x.Instructions
                .OrderBy(i => i.StepOrder)
                .Select(i => i.Content)
                .ToList(),
            Tags = x.Tags
                .Select(t => t.Tag)
                .ToList(),
            Media = x.Media
                .Select(m => new ExerciseMediaItemDto { Url = m.Url, Type = m.Type })
                .ToList(),
            Warnings = x.Warnings
                .Select(w => w.Content)
                .ToList()
        };
}
