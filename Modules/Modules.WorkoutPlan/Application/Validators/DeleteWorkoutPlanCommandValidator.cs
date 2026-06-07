using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class DeleteWorkoutPlanCommandValidator : AbstractValidator<DeleteWorkoutPlanCommand>
{
    public DeleteWorkoutPlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
