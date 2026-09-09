using Kernel.Entities;

namespace Cms.Entities;

/// <summary>
/// Tag entity for categorizing and filtering content items.
/// </summary>
public class ContentTag : IAuditable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Display name (e.g., "Getting Started").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe slug (e.g., "getting-started"). Unique.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    // Navigations
    public ICollection<ContentItemTag> ContentItems { get; set; } = [];

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
