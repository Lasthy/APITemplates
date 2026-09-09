using Kernel.Events;

namespace Cms.Contracts.Events;

/// <summary>
/// Published when media is uploaded to the CMS storage.
/// </summary>
public record MediaUploadedEvent(
    DefaultId MediaId,
    DefaultId? ContentId,
    string FileName,
    string MimeType,
    long SizeBytes) : Event;
