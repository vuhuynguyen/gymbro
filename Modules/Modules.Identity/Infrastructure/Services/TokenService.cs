using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Infrastructure.Services;

public class TokenService(IConfiguration configuration)
{
    public string GenerateToken(AppUser user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!));

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("domainUserId", user.DomainUserId.ToString()),
            new("is_admin", user.IsPlatformAdmin.ToString().ToLowerInvariant()),
            // SecurityStamp snapshot — validated per-request so rotating the stamp instantly
            // revokes every live access token for this user (see Program.cs OnTokenValidated).
            new("stamp", user.SecurityStamp ?? string.Empty)
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim("email", user.Email));

        if (!string.IsNullOrEmpty(user.PhoneNumber))
            claims.Add(new Claim("phone", user.PhoneNumber));

        var accessMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(accessMinutes),
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
