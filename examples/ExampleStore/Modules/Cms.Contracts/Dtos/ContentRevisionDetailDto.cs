namespace Cms.Contracts.Dtos;

/// <summary>
/// Point-in-time snapshot of all content item fields at a specific revision.
/// </summary>
public record ContentRevisionDetailDto(
    DefaultId Id,
    int RevisionNumber,
    string Title,
    string? Body,
    string? Excerpt,
    string? FieldsJson,
    string? MetadataJson,
    DefaultId AuthorId,
    string? ChangeNote,
    DateTime CreatedAt);
