using Cms.Contracts.Constants;

namespace Cms.ContentTypes.BuiltIn;

/// <summary>
/// Built-in content type for blog posts with featured image and excerpt.
/// </summary>
public class PostContentType : IContentType
{
    public static string Name => "Post";
    public static string Description => "A blog post with featured image and excerpt.";
    public static string Icon => "article";
    public static bool HasBody => true;
    public static string BodyFormat => "markdown";

    public static IReadOnlyList<ContentField> Fields =>
    [
        new("FeaturedImageUrl", "Featured Image", ContentFieldType.Image),
        new("IsFeatured", "Featured Post", ContentFieldType.Boolean, DefaultValue: "false")
    ];

    public static bool SupportsTags => true;
    public static bool SupportsMedia => true;
    public static int? MaxRevisions => 50;
}
