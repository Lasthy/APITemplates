using Kernel.Events;

namespace Cms.Contracts.Events;

/// <summary>
/// Published when a content item is updated, creating a new revision.
/// </summary>
public record ContentUpdatedEvent(
    DefaultId ContentId,
    string ContentType,
    int RevisionNumber) : Event;
