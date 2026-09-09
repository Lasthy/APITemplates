using Cms.Contracts.Constants;

namespace Cms.ContentTypes.BuiltIn;

/// <summary>
/// Built-in content type for FAQ entries with question and answer fields.
/// </summary>
public class FaqContentType : IContentType
{
    public static string Name => "Faq";
    public static string Description => "FAQ entry with question and answer.";
    public static string Icon => "help";
    public static bool HasBody => false;
    public static string BodyFormat => "plaintext";

    public static IReadOnlyList<ContentField> Fields =>
    [
        new("Question", "Question", ContentFieldType.Text, IsRequired: true, MaxLength: 500),
        new("Answer", "Answer", ContentFieldType.Markdown, IsRequired: true)
    ];

    public static bool SupportsTags => true;
    public static bool SupportsMedia => false;
    public static int? MaxRevisions => 20;
}
