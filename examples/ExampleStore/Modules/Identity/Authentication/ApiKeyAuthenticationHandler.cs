using System.Security.Claims;
using System.Text.Encodings.Web;
using Identity.Contracts.Constants;
using Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Authentication;

/// <summary>
/// Options for API Key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";

    /// <summary>
    /// The HTTP header name to read the API key from. Defaults to <c>X-API-Key</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-API-Key";
}

/// <summary>
/// ASP.NET Core <see cref="AuthenticationHandler{TOptions}"/> that authenticates requests
/// via an API key passed in the <c>X-API-Key</c> header.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = Options.HeaderName ?? "X-API-Key";

        if (!Request.Headers.TryGetValue(headerName, out var keyHeaderValues) || keyHeaderValues.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var rawKey = keyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return AuthenticateResult.Fail("API key is empty.");
        }

        var validationResult = await _apiKeyService.ValidateKeyAsync(rawKey, Context.RequestAborted);
        if (validationResult.IsFailure)
        {
            return AuthenticateResult.Fail(validationResult.Error?.Message ?? "Invalid API key.");
        }

        var result = validationResult.Value!;
        var user = result.User;
        var apiKey = result.ApiKey;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new("name", user.UserName ?? string.Empty),
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
            claims.Add(new Claim("email", user.Email));
        }

        if (!string.IsNullOrEmpty(user.ProfileName))
        {
            claims.Add(new Claim(IdentityClaimTypes.Profile, user.ProfileName));
        }

        if (user.IsDummy)
        {
            claims.Add(new Claim(IdentityClaimTypes.IsDummy, "true"));
        }

        foreach (var role in result.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        foreach (var (k, v) in result.Claims)
        {
            if (!claims.Any(c => c.Type == k))
            {
                claims.Add(new Claim(k, v));
            }
        }

        claims.Add(new Claim(IdentityClaimTypes.ApiKeyId, apiKey.Id.ToString()));
        claims.Add(new Claim(IdentityClaimTypes.ApiKeyName, apiKey.Name));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
