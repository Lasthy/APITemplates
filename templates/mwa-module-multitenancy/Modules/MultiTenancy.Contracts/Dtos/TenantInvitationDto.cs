namespace MultiTenancy.Contracts.Dtos;

/// <summary>
/// Summary DTO for a pending tenant invitation.
/// </summary>
public record TenantInvitationDto(
    DefaultId Id,
    string Email,
    string Role,
    DateTime ExpiresAt,
    bool IsAccepted);
