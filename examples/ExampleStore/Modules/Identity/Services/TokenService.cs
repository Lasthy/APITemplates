using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Configuration;
using Identity.Contracts.Constants;
using Identity.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Services;

/// <summary>
/// JWT + refresh token service backed by <see cref="IdentitySettings"/>.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IdentitySettings _settings;

    public TokenService(IdentitySettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public string GenerateAccessToken(ApplicationUser user, IList<string> roles, IList<Claim> claims)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.JwtSecret));

        var tokenClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(IdentityClaimTypes.Profile, user.ProfileName),
        };

        if (user.IsDummy)
        {
            tokenClaims.Add(new Claim(IdentityClaimTypes.IsDummy, "true"));
        }

        // Add roles as claims
        foreach (var role in roles)
        {
            tokenClaims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add user-specific claims
        foreach (var claim in claims)
        {
            // Avoid duplicating standard claims
            if (claim.Type != ClaimTypes.NameIdentifier &&
                claim.Type != ClaimTypes.Name &&
                claim.Type != ClaimTypes.Role)
            {
                tokenClaims.Add(claim);
            }
        }

        var token = new JwtSecurityToken(
            issuer: _settings.JwtIssuer,
            audience: _settings.JwtAudience,
            claims: tokenClaims,
            expires: GetAccessTokenExpiration(),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc/>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <inheritdoc/>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false, // We want to read expired tokens
            ValidIssuer = _settings.JwtIssuer,
            ValidAudience = _settings.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_settings.JwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public DateTime GetAccessTokenExpiration() =>
        DateTime.UtcNow.AddMinutes(_settings.AccessTokenLifetimeMinutes);
}
