using System.Security.Claims;
using Identity.Entities;

namespace Identity.Services;

/// <summary>
/// Service for generating and validating JWT access tokens and refresh tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user with the given roles and claims.
    /// </summary>
    /// <param name="user">The user to generate the token for.</param>
    /// <param name="roles">The user's roles to include as claims.</param>
    /// <param name="claims">Additional user claims.</param>
    /// <returns>The signed JWT string.</returns>
    string GenerateAccessToken(ApplicationUser user, IList<string> roles, IList<Claim> claims);

    /// <summary>
    /// Generates a cryptographically random refresh token string.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Extracts the <see cref="ClaimsPrincipal"/> from an expired JWT access token
    /// (ignoring lifetime validation). Returns <c>null</c> if the token is invalid.
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

    /// <summary>
    /// Gets the access token expiration timestamp based on current settings.
    /// </summary>
    DateTime GetAccessTokenExpiration();
}
