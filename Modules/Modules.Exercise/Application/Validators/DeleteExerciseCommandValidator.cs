using FluentValidation;
using Modules.ExerciseModule.Application.Commands;

namespace Modules.ExerciseModule.Application.Validators;

public class DeleteExerciseCommandValidator : AbstractValidator<DeleteExerciseCommand>
{
    public DeleteExerciseCommandValidator()
    {
        RuleFor(x => x.ExerciseId).NotEmpty();
    }
}
