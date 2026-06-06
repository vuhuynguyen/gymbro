using FluentValidation;
using Modules.WorkoutSessionModule.Application.Commands;

namespace Modules.WorkoutSessionModule.Application.Validators;

public sealed class AbandonSessionCommandValidator : AbstractValidator<AbandonSessionCommand>
{
    public AbandonSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
