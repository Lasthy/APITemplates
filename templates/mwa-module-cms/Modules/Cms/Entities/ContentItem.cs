using Kernel.Entities;

namespace Cms.Entities;

/// <summary>
/// Core content entity representing structured content.
/// </summary>
public class ContentItem : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Content type name (e.g., "Page", "Post", "Faq").
    /// Maps to a registered IContentType implementation.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Display title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe unique slug, auto-generated from title.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Primary content body. Format determined by the content type (Markdown, HTML, or plain text).
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Optional short excerpt/summary for list views and SEO.
    /// </summary>
    public string? Excerpt { get; set; }

    /// <summary>
    /// Structured fields as JSON, validated against the content type's field schema.
    /// </summary>
    public string? FieldsJson { get; set; }

    /// <summary>
    /// Lifecycle status: Draft, Published, Archived.
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Author user ID.
    /// </summary>
    public DefaultId AuthorId { get; set; }

    /// <summary>
    /// Scheduled publish date. Content with status Draft and a past PublishAt can be auto-published by a worker.
    /// </summary>
    public DateTime? PublishAt { get; set; }

    /// <summary>
    /// When this content was actually published.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Current revision number. Starts at 1, incremented on each update.
    /// </summary>
    public int RevisionNumber { get; set; } = 1;

    /// <summary>
    /// Optional ordering field for manual content ordering within a type.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Freeform metadata JSON (SEO fields, custom key-values).
    /// </summary>
    public string? MetadataJson { get; set; }

    // Navigations
    public ICollection<ContentItemTag> Tags { get; set; } = [];
    public ICollection<ContentMedia> Media { get; set; } = [];
    public ICollection<ContentRevision> Revisions { get; set; } = [];

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }
}
