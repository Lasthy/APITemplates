namespace Cms.Contracts.Dtos;

/// <summary>
/// Summary representation of a content revision for history listings.
/// </summary>
public record ContentRevisionDto(
    DefaultId Id,
    int RevisionNumber,
    DefaultId AuthorId,
    string? ChangeNote,
    DateTime CreatedAt);
