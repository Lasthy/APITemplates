namespace Cms.Contracts.Dtos;

/// <summary>
/// Data transfer object for attached or library media.
/// </summary>
public record ContentMediaDto(
    DefaultId Id,
    string FileName,
    string MimeType,
    long SizeBytes,
    string? AltText,
    int? Width,
    int? Height,
    string? PublicUrl,
    DateTime CreatedAt);
