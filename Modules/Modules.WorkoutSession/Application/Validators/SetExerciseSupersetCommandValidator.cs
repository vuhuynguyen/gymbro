using FluentValidation;
using Modules.WorkoutSessionModule.Application.Commands;

namespace Modules.WorkoutSessionModule.Application.Validators;

public sealed class SetExerciseSupersetCommandValidator : AbstractValidator<SetExerciseSupersetCommand>
{
    public SetExerciseSupersetCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.PeerExerciseId)
            .NotEqual(x => x.ExerciseId)
            .When(x => x.PeerExerciseId.HasValue)
            .WithMessage("An exercise can't be supersetted with itself.");
    }
}
