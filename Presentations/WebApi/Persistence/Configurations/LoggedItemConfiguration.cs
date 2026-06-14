using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.FoodModule.Entities;
using Modules.NutritionModule.Entities;

namespace WebApi.Persistence.Configurations;

public sealed class LoggedItemConfiguration : IEntityTypeConfiguration<LoggedItem>
{
    public void Configure(EntityTypeBuilder<LoggedItem> builder)
    {
        builder.ToTable("LoggedItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.DailyNutritionLogId).IsRequired();
        // FoodId is optional: null ⇒ an inline custom item (no catalog entry; the snapshot carries it).
        builder.Property(x => x.FoodId);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(20);
        builder.Property(x => x.MealName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ScheduledTime);
        builder.Property(x => x.Order).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(8, 2).IsRequired();

        // Durable snapshot captured at seed/log time so a later food edit never rewrites a closed day
        // (mirrors PerformedExercise.ExerciseName).
        builder.Property(x => x.FoodNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ServingLabelSnapshot).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EnergyKcal).HasPrecision(8, 2);
        builder.Property(x => x.ProteinG).HasPrecision(8, 2);
        builder.Property(x => x.CarbsG).HasPrecision(8, 2);
        builder.Property(x => x.FatG).HasPrecision(8, 2);
        builder.Property(x => x.FiberG).HasPrecision(8, 2);

        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        // Offline idempotency: a client-generated id for ad-hoc creates. Planned/seeded items have none.
        builder.Property(x => x.ClientItemId);

        // Current food: cross-module FK to the catalog (Restrict), mirroring PerformedExercise → Exercise.
        // SubstitutedFromFoodId is an app-enforced soft FK (no DB FK), mirroring SubstitutedFromExerciseId.
        builder.HasOne<Food>()
            .WithMany()
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DailyNutritionLog>()
            .WithMany(l => l.Items)
            .HasForeignKey(x => x.DailyNutritionLogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.DailyNutritionLogId, x.Order });
        builder.HasIndex(x => new { x.DailyNutritionLogId, x.Status });

        // One row per (day, client id) — the DB backstop for idempotent offline replays.
        builder.HasIndex(x => new { x.DailyNutritionLogId, x.ClientItemId })
            .IsUnique()
            .HasFilter("\"ClientItemId\" IS NOT NULL");
    }
}
