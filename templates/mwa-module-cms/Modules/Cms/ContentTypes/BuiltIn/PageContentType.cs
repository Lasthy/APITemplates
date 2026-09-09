namespace Cms.ContentTypes.BuiltIn;

/// <summary>
/// Built-in content type for a generic content page.
/// </summary>
public class PageContentType : IContentType
{
    public static string Name => "Page";
    public static string Description => "A generic content page.";
    public static string Icon => "page";
    public static bool HasBody => true;
    public static string BodyFormat => "markdown";
    public static IReadOnlyList<ContentField> Fields => [];
    public static bool SupportsTags => true;
    public static bool SupportsMedia => true;
    public static int? MaxRevisions => null;
}
