using FluentValidation;
using Modules.UserModule.Application.Commands;

namespace Modules.UserModule.Application.Validators;

public class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200);
    }
}
