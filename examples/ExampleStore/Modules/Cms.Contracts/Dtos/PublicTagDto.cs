namespace Cms.Contracts.Dtos;

/// <summary>
/// Public tag summary including published content count.
/// </summary>
public record PublicTagDto(
    DefaultId Id,
    string Name,
    string Slug,
    string? Description,
    int ContentCount);

/// <summary>
/// Public tag details including published content items.
/// </summary>
public record PublicTagDetailDto(
    DefaultId Id,
    string Name,
    string Slug,
    string? Description,
    IReadOnlyList<ContentItemDto> Contents);
