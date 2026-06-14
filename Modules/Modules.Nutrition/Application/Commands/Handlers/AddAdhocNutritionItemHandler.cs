using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Queries;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class AddAdhocNutritionItemHandler(
    INutritionDayProvisioner provisioner,
    IDailyNutritionLogRepository logRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AddAdhocNutritionItemCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddAdhocNutritionItemCommand request, CancellationToken cancellationToken)
    {
        // Get-or-create the caller's own day for the date. With no assignment, this provisions a plan-less,
        // self-logged day stamped with the active gym (this write surface is tenant-scoped), so off-plan logging
        // works even without a prescribed plan. currentUser.UserId is the only trainee id used (self-scoped):
        // a nutrition day is unique per (TraineeId, LocalDate) globally, so its TenantId is simply the gym that
        // was active when the day was first created.
        var log = await provisioner.GetOrCreateForWriteAsync(
            currentUser.UserId, request.Date, timezone: null, cancellationToken);
        if (log == null)
            return Result<Guid>.Failure(Validation(
                "Tenant",
                "Select a gym to log nutrition."));
        if (!log.IsOpen)
            return Result<Guid>.Failure(Conflict("DailyLog.Closed", "This day is closed and can no longer be edited."));

        // Idempotent create: a replayed offline write (same client-generated id) returns the existing item rather
        // than logging a duplicate — so a flaky retry or an offline-queue flush never double-counts a meal.
        if (request.ClientItemId is { } clientId && clientId != Guid.Empty)
        {
            var existing = log.Items.FirstOrDefault(i => i.ClientItemId == clientId);
            if (existing != null)
                return Result<Guid>.Success(existing.Id);
        }

        var mealName = string.IsNullOrWhiteSpace(request.MealName) ? "Off-plan" : request.MealName.Trim();
        LoggedItemData data;

        if (request.FoodId is { } foodId && foodId != Guid.Empty)
        {
            // Catalog food: resolve its snapshot + kind.
            var summariesResult = await mediator.Send(new ResolveFoodSummariesQuery([foodId]), cancellationToken);
            if (summariesResult.IsFailure)
                return Result<Guid>.Failure(summariesResult.Error);
            if (!summariesResult.Value!.TryGetValue(foodId, out var food))
                return Result<Guid>.Failure(Validation("FoodId", $"Food {foodId} was not found."));

            data = new LoggedItemData(
                PlanMealItemId: null, MealName: mealName, ScheduledTime: null, Order: 0,
                FoodId: foodId,
                Kind: food.Kind,
                FoodNameSnapshot: food.Name, ServingLabelSnapshot: food.ServingLabel, Quantity: request.Quantity,
                EnergyKcal: food.EnergyKcal, ProteinG: food.ProteinG, CarbsG: food.CarbsG,
                FatG: food.FatG, FiberG: food.FiberG, ClientItemId: request.ClientItemId);
        }
        else if (!string.IsNullOrWhiteSpace(request.CustomName))
        {
            // Inline custom food: no catalog entry — the item carries its own snapshot (FoodId null).
            data = new LoggedItemData(
                PlanMealItemId: null, MealName: mealName, ScheduledTime: null, Order: 0,
                FoodId: null,
                Kind: string.IsNullOrWhiteSpace(request.CustomKind) ? "Food" : request.CustomKind.Trim(),
                FoodNameSnapshot: request.CustomName.Trim(),
                ServingLabelSnapshot: string.IsNullOrWhiteSpace(request.ServingLabel) ? "1 serving" : request.ServingLabel.Trim(),
                Quantity: request.Quantity,
                EnergyKcal: request.EnergyKcal, ProteinG: request.ProteinG, CarbsG: request.CarbsG,
                FatG: request.FatG, FiberG: request.FiberG, ClientItemId: request.ClientItemId);
        }
        else
        {
            return Result<Guid>.Failure(Validation("FoodId", "Provide a catalog FoodId or a custom food name."));
        }

        var item = log.AddAdhocItem(data, request.Note);
        // Register the new item explicitly. When the day already existed it is tracked Unchanged, and a child
        // reached only through its navigation collection is mis-tracked Modified (a 0-row UPDATE) rather than
        // Added — so a second write to an existing day must force the child Added to insert cleanly.
        logRepository.AddItem(item);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(item.Id);
        }
        catch (DbUpdateException)
        {
            // Race: a concurrent first-touch created the day for this (trainee, date) between the provisioner's
            // existence check and our save (the unique TraineeId+LocalDate index rejects our insert). Mirrors
            // StartSessionHandler's duplicate-key handling — but a nutrition day is shared, not exclusive, so
            // recover onto the winning day and re-apply the item rather than returning a conflict.
            var raced = await logRepository.GetOwnByDateAsync(currentUser.UserId, request.Date, cancellationToken);
            if (raced == null || raced.Id == log.Id)
                throw; // Not the day-uniqueness race — surface the real failure.

            logRepository.Detach(log); // drop our losing insert so the next save only writes the new item
            if (!raced.IsOpen)
                return Result<Guid>.Failure(Conflict("DailyLog.Closed", "This day is closed and can no longer be edited."));

            // Idempotency holds on the winning day too (a concurrent flush may already have applied this create).
            if (request.ClientItemId is { } cid && cid != Guid.Empty)
            {
                var existingOnRaced = raced.Items.FirstOrDefault(i => i.ClientItemId == cid);
                if (existingOnRaced != null)
                    return Result<Guid>.Success(existingOnRaced.Id);
            }

            var retryItem = raced.AddAdhocItem(data, request.Note);
            logRepository.AddItem(retryItem); // raced is loaded (Unchanged) — force the child Added, as above
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(retryItem.Id);
        }
    }
}
