namespace MultiTenancy.Contracts.Dtos;

/// <summary>
/// Summary DTO for a tenant member.
/// </summary>
public record TenantMemberDto(
    DefaultId UserId,
    string Role,
    DateTime JoinedAt);
