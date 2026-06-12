using FluentValidation;
using Modules.NutritionModule.Application.Commands;

namespace Modules.NutritionModule.Application.Validators;

public sealed class CreateNutritionPlanCommandValidator : AbstractValidator<CreateNutritionPlanCommand>
{
    public CreateNutritionPlanCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}

public sealed class DeleteNutritionPlanCommandValidator : AbstractValidator<DeleteNutritionPlanCommand>
{
    public DeleteNutritionPlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class SetNutritionPlanArchivedCommandValidator : AbstractValidator<SetNutritionPlanArchivedCommand>
{
    public SetNutritionPlanArchivedCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class ReplaceNutritionPlanStructureCommandValidator
    : AbstractValidator<ReplaceNutritionPlanStructureCommand>
{
    public ReplaceNutritionPlanStructureCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.Meals).NotNull();

        RuleForEach(x => x.Meals).ChildRules(m =>
        {
            m.RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            m.RuleFor(x => x.Order).GreaterThanOrEqualTo(1);
            m.RuleFor(x => x.DayApplicability).IsInEnum();
            m.RuleFor(x => x.Items).NotNull();
            m.RuleForEach(x => x.Items).ChildRules(i =>
            {
                i.RuleFor(x => x.FoodId).NotEmpty();
                i.RuleFor(x => x.Order).GreaterThanOrEqualTo(1);
                i.RuleFor(x => x.Quantity).GreaterThan(0);
            });
        });
    }
}
