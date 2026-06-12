using FluentValidation;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Validators;

public sealed class LogMetricEntryCommandValidator : AbstractValidator<LogMetricEntryCommand>
{
    // Sensible bound matching the numeric(8,2) column: covers any plausible body metric
    // (weight, sleep hours, water ml, steps-lite, …) without accepting garbage.
    private const decimal MaxValue = 999_999.99m;

    public LogMetricEntryCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .MaximumLength(MetricEntry.TypeMaxLength);

        RuleFor(x => x.Value)
            .InclusiveBetween(0m, MaxValue)
            .WithMessage($"Value must be between 0 and {MaxValue}.");

        RuleFor(x => x.Unit)
            .MaximumLength(MetricEntry.UnitMaxLength)
            .When(x => x.Unit != null);

        // A bound model-bound DateOnly is structurally valid; reject only absurd dates.
        RuleFor(x => x.LocalDate)
            .Must(d => d!.Value.Year is >= 2000 and <= 2100)
            .WithMessage("LocalDate must be a plausible calendar date.")
            .When(x => x.LocalDate.HasValue);
    }
}
