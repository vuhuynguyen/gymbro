using FluentValidation;
using Modules.NutritionModule.Application.Commands;

namespace Modules.NutritionModule.Application.Validators;

public sealed class CreateNutritionAssignmentCommandValidator : AbstractValidator<CreateNutritionAssignmentCommand>
{
    public CreateNutritionAssignmentCommandValidator()
    {
        RuleFor(x => x.TraineeId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.VisibilityMode).IsInEnum();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date cannot precede the start date.");
    }
}
