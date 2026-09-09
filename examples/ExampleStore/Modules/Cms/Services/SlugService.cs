using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Cms.Configuration;
using Cms.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Services;

/// <summary>
/// Default implementation of ISlugService.
/// Handles Unicode diacritics removal, URL normalization, and uniqueness checking per content type.
/// </summary>
public partial class SlugService : ISlugService
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly CmsSettings _settings;

    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"[\s-]+", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespaceOrHyphenRegex();

    public SlugService(CmsModuleDbContext dbContext, CmsSettings settings)
    {
        _dbContext = dbContext;
        _settings = settings;
    }

    public string Generate(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "content";
        }

        // Replace separators like /, \, _ with hyphen
        var preprocessed = title
            .Replace('/', '-')
            .Replace('\\', '-')
            .Replace('_', '-');

        var lower = preprocessed.ToLowerInvariant();

        // Specific transliterations (must be before diacritics removal)
        lower = lower
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss")
            .Replace("ø", "oe")
            .Replace("æ", "ae")
            .Replace("å", "aa");

        var normalizedString = RemoveDiacritics(lower);

        // Remove invalid characters
        var cleaned = NonAlphanumericRegex().Replace(normalizedString, "");

        // Convert multiple spaces/hyphens into single hyphen
        var slug = MultipleWhitespaceOrHyphenRegex().Replace(cleaned, "-").Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "content";
        }

        var maxLength = _settings.MaxSlugLength > 0 ? _settings.MaxSlugLength : 200;
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug;
    }

    public async Task<string> EnsureUniqueAsync(
        string slug,
        string contentType,
        DefaultId? excludeContentId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var baseSlug = slug;
        var candidateSlug = baseSlug;
        var counter = 1;
        var maxLength = _settings.MaxSlugLength > 0 ? _settings.MaxSlugLength : 200;

        while (true)
        {
            var exists = await _dbContext.ContentItems
                .AsNoTracking()
                .AnyAsync(ci =>
                    ci.ContentType == contentType &&
                    ci.Slug == candidateSlug &&
                    (!excludeContentId.HasValue || ci.Id != excludeContentId.Value) &&
                    ci.DeletedAt == null, ct);

            if (!exists)
            {
                return candidateSlug;
            }

            counter++;
            var suffix = $"-{counter}";
            var allowedBaseLength = Math.Max(1, maxLength - suffix.Length);
            var trimmedBase = baseSlug.Length > allowedBaseLength
                ? baseSlug[..allowedBaseLength].TrimEnd('-')
                : baseSlug;

            candidateSlug = $"{trimmedBase}{suffix}";
        }
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
