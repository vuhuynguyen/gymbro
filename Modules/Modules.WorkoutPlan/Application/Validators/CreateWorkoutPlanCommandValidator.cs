using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class CreateWorkoutPlanCommandValidator : AbstractValidator<CreateWorkoutPlanCommand>
{
    public CreateWorkoutPlanCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description != null);

        RuleFor(x => x.DurationWeeks)
            .InclusiveBetween(2, 4)
            .When(x => x.DurationWeeks.HasValue);

        RuleFor(x => x.WorkoutsPerWeek)
            .InclusiveBetween(3, 6)
            .When(x => x.WorkoutsPerWeek.HasValue);
    }
}
