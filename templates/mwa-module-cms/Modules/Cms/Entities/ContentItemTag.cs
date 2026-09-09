namespace Cms.Entities;

/// <summary>
/// Many-to-many join entity between ContentItem and ContentTag.
/// </summary>
public class ContentItemTag
{
    public DefaultId ContentItemId { get; set; }
    public DefaultId ContentTagId { get; set; }

    // Navigations
    public ContentItem ContentItem { get; set; } = null!;
    public ContentTag ContentTag { get; set; } = null!;
}
