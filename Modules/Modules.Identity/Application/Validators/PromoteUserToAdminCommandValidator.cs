using FluentValidation;
using Modules.IdentityModule.Application.Commands;

namespace Modules.IdentityModule.Application.Validators;

public sealed class PromoteUserToAdminCommandValidator : AbstractValidator<PromoteUserToAdminCommand>
{
    public PromoteUserToAdminCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
