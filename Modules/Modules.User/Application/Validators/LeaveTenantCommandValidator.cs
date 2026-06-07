using FluentValidation;
using Modules.UserModule.Application.Commands;

namespace Modules.UserModule.Application.Validators;

public class LeaveTenantCommandValidator : AbstractValidator<LeaveTenantCommand>
{
    public LeaveTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
