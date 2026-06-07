using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class DeletePlanAssignmentCommandValidator : AbstractValidator<DeletePlanAssignmentCommand>
{
    public DeletePlanAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}
