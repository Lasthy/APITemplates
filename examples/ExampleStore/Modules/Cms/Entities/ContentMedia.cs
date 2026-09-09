using Kernel.Entities;

namespace Cms.Entities;

/// <summary>
/// Media attachment linked to a content item or stored in the media library.
/// </summary>
public class ContentMedia : IAuditable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// The content item this media is attached to. Null for unattached/library media.
    /// </summary>
    public DefaultId? ContentItemId { get; set; }

    /// <summary>
    /// Original file name as uploaded.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Storage path/key in the Kernel's IFileStorage.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// MIME type (e.g., "image/jpeg", "application/pdf").
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Alt text for accessibility (images).
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Image width in pixels (null for non-image files).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Image height in pixels (null for non-image files).
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Public URL for accessing this media file.
    /// </summary>
    public string? PublicUrl { get; set; }

    // Navigation
    public ContentItem? ContentItem { get; set; }

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
