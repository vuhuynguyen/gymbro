using FluentValidation;
using Modules.FoodModule.Application.Commands;
using Modules.FoodModule.Entities;

namespace Modules.FoodModule.Application.Validators;

/// <summary>Field-level validation for a food payload (range-checks; existence/uniqueness live in the handler).</summary>
public sealed class FoodInputValidator : AbstractValidator<FoodInput>
{
    public FoodInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind)
            .NotEmpty()
            .Must(s => Enum.TryParse<FoodKind>(s, true, out _))
            .WithMessage("Invalid food kind.");
        RuleFor(x => x.ServingLabel).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Brand).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Brand));

        RuleFor(x => x.ServingSizeGrams).GreaterThan(0).When(x => x.ServingSizeGrams.HasValue);
        RuleFor(x => x.EnergyKcal).GreaterThanOrEqualTo(0).When(x => x.EnergyKcal.HasValue);
        RuleFor(x => x.ProteinG).GreaterThanOrEqualTo(0).When(x => x.ProteinG.HasValue);
        RuleFor(x => x.CarbsG).GreaterThanOrEqualTo(0).When(x => x.CarbsG.HasValue);
        RuleFor(x => x.FatG).GreaterThanOrEqualTo(0).When(x => x.FatG.HasValue);
        RuleFor(x => x.FiberG).GreaterThanOrEqualTo(0).When(x => x.FiberG.HasValue);
    }
}

public sealed class CreateFoodCommandValidator : AbstractValidator<CreateFoodCommand>
{
    public CreateFoodCommandValidator()
    {
        RuleFor(x => x.Food).NotNull().SetValidator(new FoodInputValidator());
    }
}

public sealed class CreateCustomFoodCommandValidator : AbstractValidator<CreateCustomFoodCommand>
{
    public CreateCustomFoodCommandValidator()
    {
        RuleFor(x => x.Food).NotNull().SetValidator(new FoodInputValidator());
    }
}

public sealed class UpdateFoodCommandValidator : AbstractValidator<UpdateFoodCommand>
{
    public UpdateFoodCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Food).NotNull().SetValidator(new FoodInputValidator());
    }
}
