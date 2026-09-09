namespace Cms.Contracts.Dtos;

/// <summary>
/// Detailed representation of a registered content type including its field schema.
/// </summary>
public record ContentTypeDetailDto(
    string Name,
    string Description,
    string Icon,
    bool HasBody,
    string BodyFormat,
    bool SupportsTags,
    bool SupportsMedia,
    int? MaxRevisions,
    IReadOnlyList<ContentFieldDto> Fields);
