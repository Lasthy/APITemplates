namespace Cms.Contracts.Dtos;

/// <summary>
/// Summary representation of a registered content type.
/// </summary>
public record ContentTypeDto(
    string Name,
    string Description,
    string Icon,
    bool HasBody);
