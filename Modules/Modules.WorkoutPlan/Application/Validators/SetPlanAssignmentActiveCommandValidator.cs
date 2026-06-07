using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class SetPlanAssignmentActiveCommandValidator : AbstractValidator<SetPlanAssignmentActiveCommand>
{
    public SetPlanAssignmentActiveCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}
