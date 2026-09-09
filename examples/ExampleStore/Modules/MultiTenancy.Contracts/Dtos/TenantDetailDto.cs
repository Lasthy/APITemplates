namespace MultiTenancy.Contracts.Dtos;

/// <summary>
/// Detailed DTO for a tenant organization including member count, metadata, and lifecycle info.
/// </summary>
public record TenantDetailDto(
    DefaultId Id,
    string Name,
    string Slug,
    string Status,
    string? Plan,
    string? MetadataJson,
    DefaultId OwnerUserId,
    int MemberCount,
    DateTime CreatedAt,
    DateTime? SuspendedAt);
