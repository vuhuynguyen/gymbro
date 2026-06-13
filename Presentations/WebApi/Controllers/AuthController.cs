using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Modules.IdentityModule.Application.Commands;
using Modules.IdentityModule.Application.Models;
using Modules.UserModule.Application.Queries;
using WebApi.Requests.Auth;
using WebApi.Http;

namespace WebApi.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    // httpOnly cookie carrying the opaque refresh token. Scoped to /api/auth so it never rides along
    // on ordinary API calls. The access token, by contrast, lives only in the SPA's memory.
    private const string RefreshCookieName = "gymbro_refresh";
    private const string RefreshCookiePath = "/api/auth";

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand cmd)
    {
        var result = await mediator.Send(cmd with { Ip = CallerIp() });
        if (result.IsFailure)
            return BadRequest(result.Error);
        return IssueTokens(result.Value!);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand cmd)
    {
        var result = await mediator.Send(cmd with { Ip = CallerIp() });
        if (result.IsFailure)
            return BadRequest(result.Error);
        return IssueTokens(result.Value!);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> Refresh()
    {
        var raw = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(raw))
            return Unauthorized();

        var result = await mediator.Send(new RefreshTokenCommand(raw) { Ip = CallerIp() });
        if (result.IsFailure)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        return IssueTokens(result.Value!);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await mediator.Send(new LogoutCommand(Request.Cookies[RefreshCookieName]));
        ClearRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var result = await mediator.Send(new RevokeAllSessionsCommand());
        ClearRefreshCookie();
        if (result.IsFailure)
            return result.ToFailureResult(this);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await mediator.Send(new RequestPasswordResetCommand(request.Email));
        if (result.IsFailure)
            return BadRequest(result.Error.Message);
        return Ok(new { success = true });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await mediator.Send(
            new ResetPasswordCommand(request.Email, request.Token, request.NewPassword));
        if (result.IsFailure)
            return BadRequest(result.Error.Message);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var email = User.FindFirst("email")?.Value;
        var result = await mediator.Send(new GetMeQuery(email));
        if (result.IsFailure)
            return result.ToFailureResult(this);
        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand cmd)
    {
        var result = await mediator.Send(cmd);
        if (result.IsFailure)
            return result.ToFailureResult(this);
        // The user's other sessions were just revoked server-side; drop this connection's cookie too.
        ClearRefreshCookie();
        return NoContent();
    }

    private IActionResult IssueTokens(TokenPair tokens)
    {
        SetRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresUtc);
        return Ok(new { token = tokens.AccessToken });
    }

    private void SetRefreshCookie(string token, DateTime expiresUtc) =>
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,        // sent only over HTTPS in prod; http localhost still works in dev
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = expiresUtc
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath
        });

    private string? CallerIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
