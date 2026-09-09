namespace Cms.Services;

/// <summary>
/// Service for generating URL-safe unique slugs from titles.
/// </summary>
public interface ISlugService
{
    /// <summary>
    /// Generates a URL-safe slug from a title.
    /// Handles Unicode transliteration, special character removal, and whitespace normalization.
    /// </summary>
    string Generate(string title);

    /// <summary>
    /// Ensures the slug is unique within the content type.
    /// Appends "-2", "-3", etc. if a conflict exists.
    /// </summary>
    Task<string> EnsureUniqueAsync(
        string slug,
        string contentType,
        DefaultId? excludeContentId = null,
        CancellationToken ct = default);
}
