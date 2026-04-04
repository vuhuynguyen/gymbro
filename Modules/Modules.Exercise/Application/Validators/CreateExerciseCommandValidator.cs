using FluentValidation;
using Modules.ExerciseModule.Application.Commands;

namespace Modules.ExerciseModule.Application.Validators;

public class CreateExerciseCommandValidator 
    : AbstractValidator<CreateExerciseCommand>
{
    public CreateExerciseCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MuscleGroup)
            .NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }
}