namespace Cms.Contracts.Dtos;

/// <summary>
/// Data transfer object for a content tag.
/// </summary>
public record ContentTagDto(
    DefaultId Id,
    string Name,
    string Slug,
    string? Description);
