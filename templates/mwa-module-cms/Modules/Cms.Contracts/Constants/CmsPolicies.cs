namespace Cms.Contracts.Constants;

/// <summary>
/// Authorization policy names for the CMS module.
/// </summary>
public static class CmsPolicies
{
    /// <summary>
    /// Required to create, update, delete, tag, and manage content, types, and media.
    /// </summary>
    public const string ManageContent = "ManageContent";

    /// <summary>
    /// Required to publish, unpublish, and archive content items.
    /// </summary>
    public const string PublishContent = "PublishContent";
}
