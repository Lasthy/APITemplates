namespace Identity.Contracts.Dtos;

/// <summary>
/// Authentication token response returned after login or token refresh.
/// </summary>
public record AuthTokenDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
