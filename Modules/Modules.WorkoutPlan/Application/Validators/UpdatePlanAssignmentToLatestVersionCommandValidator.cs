using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class UpdatePlanAssignmentToLatestVersionCommandValidator
    : AbstractValidator<UpdatePlanAssignmentToLatestVersionCommand>
{
    public UpdatePlanAssignmentToLatestVersionCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        // Bound the opaque client-supplied snapshot blob so it can't store an oversized payload. (Audit finding 16.)
        RuleFor(x => x.SnapshotJson!).MaximumLength(200_000).When(x => x.SnapshotJson is not null);
    }
}
