using FluentValidation;
using Modules.UserModule.Application.Admin.Commands;

namespace Modules.UserModule.Application.Admin.Validators;

public class AdminDeleteTenantCommandValidator : AbstractValidator<AdminDeleteTenantCommand>
{
    public AdminDeleteTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
