namespace Identity.Contracts.Dtos;

/// <summary>
/// Public summary DTO representing a user.
/// Safe to share across module boundaries.
/// </summary>
public record UserDto(
    DefaultId Id,
    string Email,
    string UserName,
    string ProfileName,
    bool IsBanned,
    bool IsDummy,
    DateTime CreatedAt,
    DateTime? DummyExpiresAt = null);
