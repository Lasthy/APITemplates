namespace Identity.Contracts.Dtos;

/// <summary>
/// Summary DTO for a profile definition.
/// Used in profile listing endpoints.
/// </summary>
public record ProfileDto(
    string Name,
    string Description,
    bool IsPublic);
