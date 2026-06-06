using FluentValidation;
using Modules.UserModule.Application.Admin.Commands;

namespace Modules.UserModule.Application.Admin.Validators;

public class AdminRemoveMemberCommandValidator : AbstractValidator<AdminRemoveMemberCommand>
{
    public AdminRemoveMemberCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
