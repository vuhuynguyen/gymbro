using FluentValidation;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Validators;

public sealed class SetNutritionItemStatusCommandValidator : AbstractValidator<SetNutritionItemStatusCommand>
{
    public SetNutritionItemStatusCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => s is LoggedItemStatus.Completed or LoggedItemStatus.Skipped or LoggedItemStatus.Planned)
            .WithMessage("Status must be Completed, Skipped, or Planned.");
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note != null);
    }
}

public sealed class SubstituteNutritionItemCommandValidator : AbstractValidator<SubstituteNutritionItemCommand>
{
    public SubstituteNutritionItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.FoodId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity.HasValue);
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note != null);
    }
}

public sealed class AddAdhocNutritionItemCommandValidator : AbstractValidator<AddAdhocNutritionItemCommand>
{
    public AddAdhocNutritionItemCommandValidator()
    {
        // Either a catalog food OR an inline custom food (name) — exactly the two add paths.
        RuleFor(x => x)
            .Must(x => (x.FoodId.HasValue && x.FoodId != Guid.Empty) || !string.IsNullOrWhiteSpace(x.CustomName))
            .WithMessage("Provide a catalog FoodId or a custom food name.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.CustomName).MaximumLength(200).When(x => x.CustomName != null);
        RuleFor(x => x.ServingLabel).MaximumLength(100).When(x => x.ServingLabel != null);
        RuleFor(x => x.MealName).MaximumLength(200).When(x => x.MealName != null);
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note != null);
    }
}

public sealed class RemoveNutritionItemCommandValidator : AbstractValidator<RemoveNutritionItemCommand>
{
    public RemoveNutritionItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
    }
}
