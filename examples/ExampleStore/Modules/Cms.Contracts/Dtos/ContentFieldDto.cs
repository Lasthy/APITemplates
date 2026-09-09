namespace Cms.Contracts.Dtos;

/// <summary>
/// Definition of a custom structured field within a content type schema.
/// </summary>
public record ContentFieldDto(
    string Name,
    string DisplayName,
    string FieldType,
    bool IsRequired,
    string? DefaultValue,
    string? Description,
    int? MaxLength,
    string? ValidationRegex);
