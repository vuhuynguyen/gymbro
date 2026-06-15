using FluentValidation;
using Modules.WorkoutSessionModule.Application.Commands;

namespace Modules.WorkoutSessionModule.Application.Validators;

public sealed class CompleteSessionCommandValidator : AbstractValidator<CompleteSessionCommand>
{
    public CompleteSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.RpeOverall).InclusiveBetween(1, 10).When(x => x.RpeOverall.HasValue);
        // A completion timestamp in the future is invalid (the entity also clamps as defense-in-depth);
        // the lower bound (>= StartedAt) needs the loaded session, so it is enforced in the entity.
        RuleFor(x => x.CompletedAt!.Value)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(1))
            .When(x => x.CompletedAt.HasValue)
            .WithMessage("CompletedAt cannot be in the future.");
    }
}
