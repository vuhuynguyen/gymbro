using FluentValidation;
using Modules.WorkoutSessionModule.Application.Commands;

namespace Modules.WorkoutSessionModule.Application.Validators;

public sealed class DeleteSetCommandValidator : AbstractValidator<DeleteSetCommand>
{
    public DeleteSetCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.SetId).NotEmpty();
    }
}
