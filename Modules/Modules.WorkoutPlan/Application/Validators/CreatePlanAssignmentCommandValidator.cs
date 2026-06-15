using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class CreatePlanAssignmentCommandValidator : AbstractValidator<CreatePlanAssignmentCommand>
{
    public CreatePlanAssignmentCommandValidator()
    {
        RuleFor(x => x.TraineeId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.FrequencyDaysPerWeek).InclusiveBetween(1, 7);
        RuleFor(x => x.VisibilityMode).IsInEnum();
        // Bound the opaque client-supplied snapshot blob so it can't store an oversized payload. (Audit finding 16.)
        RuleFor(x => x.SnapshotJson!).MaximumLength(200_000).When(x => x.SnapshotJson is not null);
    }
}
