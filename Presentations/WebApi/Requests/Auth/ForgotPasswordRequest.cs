namespace WebApi.Requests.Auth;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = null!;
}

public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}
