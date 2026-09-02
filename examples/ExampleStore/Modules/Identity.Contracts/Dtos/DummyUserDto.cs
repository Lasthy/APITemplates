namespace Identity.Contracts.Dtos;

/// <summary>
/// Result returned when creating or recycling a dummy user, containing credentials and initial authentication tokens.
/// </summary>
public record DummyUserDto(
    DefaultId Id,
    string UserName,
    string Email,
    string Password,
    DateTime ExpiresAt,
    AuthTokenDto Tokens,
    bool IsRecycled);
