namespace Cms.ContentTypes;

/// <summary>
/// Defines a code-defined content type with its field schema.
/// Implementations are discovered by assembly scanning during module registration.
/// </summary>
public interface IContentType
{
    /// <summary>
    /// Unique name for this content type (e.g., "Page", "Post", "Faq").
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    static abstract string Description { get; }

    /// <summary>
    /// Icon identifier for UI rendering (e.g., "page", "article", "help").
    /// </summary>
    static abstract string Icon { get; }

    /// <summary>
    /// Whether this content type uses the Body field (Markdown/HTML editor).
    /// </summary>
    static abstract bool HasBody { get; }

    /// <summary>
    /// Body format: "markdown", "html", or "plaintext".
    /// </summary>
    static abstract string BodyFormat { get; }

    /// <summary>
    /// Custom field definitions beyond the standard title/body/excerpt.
    /// </summary>
    static abstract IReadOnlyList<ContentField> Fields { get; }

    /// <summary>
    /// Whether this content type supports tagging.
    /// </summary>
    static abstract bool SupportsTags { get; }

    /// <summary>
    /// Whether this content type supports media attachments.
    /// </summary>
    static abstract bool SupportsMedia { get; }

    /// <summary>
    /// Maximum number of revisions to retain. Null for unlimited.
    /// </summary>
    static abstract int? MaxRevisions { get; }
}
