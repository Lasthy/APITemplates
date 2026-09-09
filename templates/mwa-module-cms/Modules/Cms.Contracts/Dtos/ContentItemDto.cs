namespace Cms.Contracts.Dtos;

/// <summary>
/// Summary representation of a content item for lists and queries.
/// </summary>
public record ContentItemDto(
    DefaultId Id,
    string ContentType,
    string Title,
    string Slug,
    string? Excerpt,
    string Status,
    DefaultId AuthorId,
    DateTime? PublishedAt,
    int RevisionNumber,
    DateTime CreatedAt);
