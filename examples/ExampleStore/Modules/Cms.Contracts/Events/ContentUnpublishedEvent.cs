using Kernel.Events;

namespace Cms.Contracts.Events;

/// <summary>
/// Published when a content item status reverts from Published to Draft.
/// </summary>
public record ContentUnpublishedEvent(
    DefaultId ContentId,
    string ContentType,
    string Slug,
    DefaultId UnpublishedBy) : Event;
