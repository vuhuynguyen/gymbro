using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class UpdatePlanAssignmentToLatestVersionCommandValidator
    : AbstractValidator<UpdatePlanAssignmentToLatestVersionCommand>
{
    public UpdatePlanAssignmentToLatestVersionCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}
