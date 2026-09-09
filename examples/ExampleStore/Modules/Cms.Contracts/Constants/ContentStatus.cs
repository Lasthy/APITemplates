namespace Cms.Contracts.Constants;

/// <summary>
/// Lifecycle statuses for content items.
/// </summary>
public static class ContentStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Published, Archived
    };

    public static bool IsValid(string status) => ValidStatuses.Contains(status);
}
