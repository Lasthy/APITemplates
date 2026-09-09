using Cms.Contracts.Constants;

namespace Cms.ContentTypes;

/// <summary>
/// Definition of a custom schema field in an IContentType.
/// </summary>
public record ContentField(
    string Name,
    string DisplayName,
    ContentFieldType FieldType,
    bool IsRequired = false,
    string? DefaultValue = null,
    string? Description = null,
    int? MaxLength = null,
    string? ValidationRegex = null);
