using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class UpdatePlanAssignmentCommandValidator : AbstractValidator<UpdatePlanAssignmentCommand>
{
    public UpdatePlanAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.FrequencyDaysPerWeek).InclusiveBetween(1, 7);
        RuleFor(x => x.VisibilityMode).IsInEnum();
    }
}
