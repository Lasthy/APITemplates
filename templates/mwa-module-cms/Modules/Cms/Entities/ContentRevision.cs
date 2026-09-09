namespace Cms.Entities;

/// <summary>
/// Point-in-time snapshot of a ContentItem.
/// </summary>
public class ContentRevision
{
    public DefaultId Id { get; set; }
    public DefaultId ContentItemId { get; set; }

    /// <summary>
    /// Sequential revision number (1, 2, 3...).
    /// </summary>
    public int RevisionNumber { get; set; }

    /// <summary>
    /// Snapshot of the title at this revision.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the body at this revision.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Snapshot of the excerpt at this revision.
    /// </summary>
    public string? Excerpt { get; set; }

    /// <summary>
    /// Snapshot of the structured fields JSON at this revision.
    /// </summary>
    public string? FieldsJson { get; set; }

    /// <summary>
    /// Snapshot of the metadata JSON at this revision.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Optional change summary provided by the author.
    /// </summary>
    public string? ChangeNote { get; set; }

    /// <summary>
    /// Who created this revision.
    /// </summary>
    public DefaultId AuthorId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public ContentItem ContentItem { get; set; } = null!;
}
