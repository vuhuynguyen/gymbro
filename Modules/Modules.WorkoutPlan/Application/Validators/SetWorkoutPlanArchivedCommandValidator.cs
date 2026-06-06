using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class SetWorkoutPlanArchivedCommandValidator : AbstractValidator<SetWorkoutPlanArchivedCommand>
{
    public SetWorkoutPlanArchivedCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
