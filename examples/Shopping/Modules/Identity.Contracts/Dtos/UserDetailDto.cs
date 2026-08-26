namespace Identity.Contracts.Dtos;

/// <summary>
/// Detailed user DTO including roles, claims, and preferences.
/// Returned for single-user queries by admin or self.
/// </summary>
public record UserDetailDto(
    DefaultId Id,
    string Email,
    string UserName,
    string ProfileName,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, string> Claims,
    bool IsBanned,
    bool IsDummy,
    string? PreferencesJson,
    DateTime CreatedAt);
