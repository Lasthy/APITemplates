namespace MultiTenancy.Contracts.Dtos;

/// <summary>
/// Summary DTO for a tenant organization.
/// </summary>
public record TenantDto(
    DefaultId Id,
    string Name,
    string Slug,
    string Status,
    string? Plan,
    DateTime CreatedAt);
