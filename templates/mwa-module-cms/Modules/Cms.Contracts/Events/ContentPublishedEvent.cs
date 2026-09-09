using Kernel.Events;

namespace Cms.Contracts.Events;

/// <summary>
/// Published when a content item status transitions to Published.
/// </summary>
public record ContentPublishedEvent(
    DefaultId ContentId,
    string ContentType,
    string Slug,
    DefaultId PublishedBy) : Event;
