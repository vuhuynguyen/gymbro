using FluentValidation;
using Modules.IdentityModule.Application.Commands;

namespace Modules.IdentityModule.Application.Validators;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
