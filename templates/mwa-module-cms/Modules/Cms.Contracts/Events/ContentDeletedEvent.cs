using Kernel.Events;

namespace Cms.Contracts.Events;

/// <summary>
/// Published when a content item is soft-deleted.
/// </summary>
public record ContentDeletedEvent(
    DefaultId ContentId,
    string ContentType,
    string Slug,
    DefaultId DeletedBy) : Event;
