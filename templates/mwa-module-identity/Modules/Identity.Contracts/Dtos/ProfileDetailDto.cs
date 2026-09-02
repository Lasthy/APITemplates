namespace Identity.Contracts.Dtos;

/// <summary>
/// Detailed profile DTO including roles, claims, and preferences type info.
/// </summary>
public record ProfileDetailDto(
    string Name,
    string Description,
    bool IsPublic,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, string> Claims,
    string? PreferencesTypeName,
    IReadOnlyList<string>? Requirements = null);
