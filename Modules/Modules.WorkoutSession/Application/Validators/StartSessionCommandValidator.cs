using FluentValidation;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Validators;

public sealed class StartSessionCommandValidator : AbstractValidator<StartSessionCommand>
{
    public StartSessionCommandValidator()
    {
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.PlanAssignmentId)
            .NotEmpty()
            .When(x => x.Source == SessionSource.FromAssignment)
            .WithMessage("PlanAssignmentId is required when source is FromAssignment.");
        RuleFor(x => x.BodyweightKg)
            .GreaterThan(0)
            .When(x => x.BodyweightKg.HasValue);
    }
}
