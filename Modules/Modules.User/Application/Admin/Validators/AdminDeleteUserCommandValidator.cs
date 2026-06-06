using FluentValidation;
using Modules.UserModule.Application.Admin.Commands;

namespace Modules.UserModule.Application.Admin.Validators;

public class AdminDeleteUserCommandValidator : AbstractValidator<AdminDeleteUserCommand>
{
    public AdminDeleteUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
