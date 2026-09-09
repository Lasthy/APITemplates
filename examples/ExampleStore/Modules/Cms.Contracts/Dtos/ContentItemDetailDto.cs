namespace Cms.Contracts.Dtos;

/// <summary>
/// Detailed representation of a content item including body, fields, tags, and media attachments.
/// </summary>
public record ContentItemDetailDto(
    DefaultId Id,
    string ContentType,
    string Title,
    string Slug,
    string? Body,
    string? Excerpt,
    string? FieldsJson,
    string? MetadataJson,
    string Status,
    DefaultId AuthorId,
    DateTime? PublishAt,
    DateTime? PublishedAt,
    int RevisionNumber,
    int SortOrder,
    IReadOnlyList<ContentTagDto> Tags,
    IReadOnlyList<ContentMediaDto> Media,
    DateTime CreatedAt);
