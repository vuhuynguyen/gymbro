using FluentValidation;
using Modules.WorkoutSessionModule.Application.Commands;

namespace Modules.WorkoutSessionModule.Application.Validators;

public sealed class UpdatePerformedExerciseCommandValidator : AbstractValidator<UpdatePerformedExerciseCommand>
{
    public UpdatePerformedExerciseCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.SubstituteExerciseId)
            .NotEmpty()
            .When(x => x.Action == ExerciseUpdateAction.Substitute)
            .WithMessage("SubstituteExerciseId is required for substitution.");
    }
}
