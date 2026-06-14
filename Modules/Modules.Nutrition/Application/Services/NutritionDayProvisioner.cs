using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Queries;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Services;

/// <summary>
/// Get-or-create provisioner for the caller's own nutrition day. Owns the snapshot-on-touch create+seed logic
/// (assignment lookup, snapshot deserialize, kind resolution, planned-item seeding) plus the plan-less
/// self-logged fallback. Does not save — the calling handler owns the unit of work and the unique-day race.
/// </summary>
public sealed class NutritionDayProvisioner(
    IDailyNutritionLogRepository logRepository,
    INutritionPlanAssignmentRepository assignmentRepository,
    ITenantContext tenantContext,
    IMediator mediator) : INutritionDayProvisioner
{
    public async Task<DailyNutritionLog?> GetOrCreateForWriteAsync(
        Guid userId, DateOnly date, string? timezone, CancellationToken ct)
    {
        // Existing day, or the assignment-seeded create path.
        var fromAssignment = await GetOrCreateFromAssignmentAsync(userId, date, timezone, ct);
        if (fromAssignment != null)
            return fromAssignment;

        // No assignment governs the date → stamp the self-logged day with the active gym. This write surface is
        // tenant-scoped (api/nutrition/log requires X-Tenant-Id, membership-validated), so the header tenant is
        // present here. A nutrition day is unique per (TraineeId, LocalDate) globally, so its TenantId is just
        // the gym that was active when the day was first created.
        var tenantId = tenantContext.TenantId;
        if (tenantId is null)
            return null; // No active gym to stamp the day with → handler returns a clean validation failure.

        var selfLogged = DailyNutritionLog.OpenSelfLogged(userId, tenantId.Value, date, timezone);
        await logRepository.AddAsync(selfLogged, ct);
        return selfLogged;
    }

    public async Task<DailyNutritionLog?> GetOrCreateFromAssignmentAsync(
        Guid userId, DateOnly date, string? timezone, CancellationToken ct)
    {
        var existing = await logRepository.GetOwnByDateAsync(userId, date, ct);
        if (existing != null)
            return existing;

        var assignment = await assignmentRepository.QueryOwnAcrossGyms(userId)
            .Where(a => a.IsActive && a.StartDate <= date && (a.EndDate == null || a.EndDate >= date))
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefaultAsync(ct);

        if (assignment == null)
            return null;

        var log = DailyNutritionLog.Open(
            userId, assignment.TenantId!.Value, date, timezone,
            NutritionSource.FromAssignment, assignment.Id, assignment.SnapshotJson);

        var snapshot = NutritionMapping.DeserializeSnapshot(assignment.SnapshotJson);
        if (snapshot != null)
        {
            // Recurrence: seed only the meals that apply to THIS day's training/rest type. "Is today a training
            // day?" is owned by the WorkoutSession module (graceful default: rest day when there is no session).
            var isTrainingDay = await mediator.Send(new IsTrainingDayQuery(userId, date, timezone), ct);
            var kinds = await ResolveKindsAsync(snapshot, ct);
            log.SeedPlannedItems(NutritionMapping.ToSeedItems(snapshot, isTrainingDay, kinds));
        }

        await logRepository.AddAsync(log, ct);
        return log;
    }

    /// <summary>Best-effort map of the snapshot's foods to their catalog kind name. On any resolve failure
    /// the items just default to "Food" — kind is cosmetic and never blocks seeding.</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ResolveKindsAsync(
        NutritionPlanSnapshot snapshot, CancellationToken ct)
    {
        var foodIds = snapshot.Meals.SelectMany(m => m.Items.Select(i => i.FoodId)).Distinct().ToList();
        if (foodIds.Count == 0) return new Dictionary<Guid, string>();

        var result = await mediator.Send(new ResolveFoodSummariesQuery(foodIds), ct);
        var kinds = new Dictionary<Guid, string>();
        if (result.IsSuccess)
            foreach (var (id, food) in result.Value!)
                kinds[id] = food.Kind;
        return kinds;
    }
}
