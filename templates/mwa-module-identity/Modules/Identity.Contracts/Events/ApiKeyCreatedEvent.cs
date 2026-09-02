using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when a new API key has been created.
/// </summary>
public record ApiKeyCreatedEvent(
    DefaultId KeyId,
    DefaultId UserId,
    string Name,
    IReadOnlyList<string>? Scopes = null) : Event;
