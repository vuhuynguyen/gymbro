using FluentValidation;
using Modules.UserModule.Application.Commands;

namespace Modules.UserModule.Application.Validators;

public class RevokeInviteCommandValidator : AbstractValidator<RevokeInviteCommand>
{
    public RevokeInviteCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(8)
            .Matches("^[A-Z2-9]+$").WithMessage("Invalid invite code.");
    }
}
