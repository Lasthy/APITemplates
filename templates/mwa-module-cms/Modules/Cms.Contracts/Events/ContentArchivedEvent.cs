using Kernel.Events;

namespace Cms.Contracts.Events;

/// <summary>
/// Published when a content item is archived.
/// </summary>
public record ContentArchivedEvent(
    DefaultId ContentId,
    string ContentType,
    string Slug,
    DefaultId ArchivedBy) : Event;
