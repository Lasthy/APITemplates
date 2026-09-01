namespace Identity.Contracts.Dtos;

/// <summary>
/// Result returned once upon creation containing the raw, plaintext API key secret.
/// </summary>
public record ApiKeyCreatedDto(
    DefaultId Id,
    string Name,
    string Key,
    string KeyPrefix,
    DefaultId UserId,
    DateTime? ExpiresAt,
    DateTime CreatedAt);
