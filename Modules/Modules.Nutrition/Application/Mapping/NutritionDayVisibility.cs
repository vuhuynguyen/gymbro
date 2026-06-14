using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Mapping;

/// <summary>
/// Applies the governing assignment's trainee-facing visibility to a day DTO (filter-on-read; trainee path only —
/// coach/admin reads keep the full prescription, mirroring the workout module). Today that is
/// <c>HideMacroTargets</c> → <see cref="NutritionMapping.RedactPlannedMacros"/>; the coarser
/// <c>VisibilityMode</c> (Guided/Blind) plan-structure hiding is a later refinement.
/// </summary>
internal static class NutritionDayVisibility
{
    public static async Task<DailyNutritionLogDto> ApplyAsync(
        DailyNutritionLog log,
        DailyNutritionLogDto dto,
        INutritionPlanAssignmentRepository assignmentRepository,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (log.NutritionPlanAssignmentId is not { } assignmentId)
            return dto;

        var assignment = await assignmentRepository.QueryOwnAcrossGyms(userId)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        return assignment is { HideMacroTargets: true }
            ? NutritionMapping.RedactPlannedMacros(dto)
            : dto;
    }
}
