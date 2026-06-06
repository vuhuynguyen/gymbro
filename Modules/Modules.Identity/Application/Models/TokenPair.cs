namespace Modules.IdentityModule.Application.Models;

/// <summary>
/// Result of an auth flow: a short-lived access JWT plus a freshly issued opaque refresh token.
/// The controller returns the access token in the body and sets the refresh token as an httpOnly cookie.
/// </summary>
public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresUtc);
