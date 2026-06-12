using FluentValidation;
using Modules.NutritionModule.Application.Commands;

namespace Modules.NutritionModule.Application.Validators;

/// <summary>Mirrors UpdatePlanAssignmentCommandValidator, adapted to nutrition's fields (end-date window).</summary>
public sealed class UpdateNutritionAssignmentCommandValidator : AbstractValidator<UpdateNutritionAssignmentCommand>
{
    public UpdateNutritionAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.VisibilityMode).IsInEnum();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.EndDate.HasValue && x.StartDate.HasValue)
            .WithMessage("End date cannot precede the start date.");
    }
}

public sealed class DeleteNutritionAssignmentCommandValidator : AbstractValidator<DeleteNutritionAssignmentCommand>
{
    public DeleteNutritionAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}

public sealed class SetNutritionAssignmentActiveCommandValidator : AbstractValidator<SetNutritionAssignmentActiveCommand>
{
    public SetNutritionAssignmentActiveCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}
