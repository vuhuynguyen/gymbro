using FluentValidation;

namespace Modules.IdentityModule.Application.Validators;

/// <summary>Single source of truth for password complexity rules, kept in sync with Identity policy and frontend checklist.</summary>
internal static class PasswordRules
{
    // Mirrors Identity policy: RequiredLength=8, RequireUppercase, RequireLowercase, RequireDigit, RequireNonAlphanumeric
    internal static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(256)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches(@"[!@#$%^&*()\-_=+\[\]{};':""\\|,.<>/?`~]")
                .WithMessage("Password must contain at least one special character.");
}
