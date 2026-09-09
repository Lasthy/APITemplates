using Kernel.Events;

namespace Cms.Contracts.Events;

/// <summary>
/// Published when a new content item is created.
/// </summary>
public record ContentCreatedEvent(
    DefaultId ContentId,
    string ContentType,
    string Title,
    string Slug,
    DefaultId AuthorId) : Event;
