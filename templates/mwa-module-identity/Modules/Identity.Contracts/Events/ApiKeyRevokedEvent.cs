using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when an existing API key has been revoked.
/// </summary>
public record ApiKeyRevokedEvent(
    DefaultId KeyId,
    DefaultId UserId,
    string Name) : Event;
